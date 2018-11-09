using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using NGate.Extensions.RabbitMq;

namespace NGate.Framework
{
    public class RouteProvider
    {
        private readonly IDictionary<string, Action<IRouteBuilder, string, Route>> _methods;
        private readonly IDictionary<string, IExtension> _extensions;
        private readonly IServiceProvider _serviceProvider;
        private readonly IRequestProcessor _requestProcessor;
        private readonly Configuration _configuration;

        public RouteProvider(IServiceProvider serviceProvider, IRequestProcessor requestProcessor,
            Configuration configuration)
        {
            _serviceProvider = serviceProvider;
            _requestProcessor = requestProcessor;
            _configuration = configuration;
            var processors = new Dictionary<string, Func<Route, Func<HttpRequest, HttpResponse, RouteData, Task>>>
            {
                ["local"] = UseLocalTypeAsync,
                ["request"] = UseRequestTypeAsync,
                ["dispatcher"] = UseDispatcherTypeAsync
            };
            _methods = new Dictionary<string, Action<IRouteBuilder, string, Route>>
            {
                ["get"] = (builder, path, route) => builder.MapGet(path, processors[route.Type](route)),
                ["post"] = (builder, path, route) => builder.MapPost(path, processors[route.Type](route)),
                ["put"] = (builder, path, route) => builder.MapPut(path, processors[route.Type](route)),
                ["delete"] = (builder, path, route) => builder.MapDelete(path, processors[route.Type](route)),
                ["patch"] = (builder, path, route) => builder.MapVerb("patch", path, processors[route.Type](route))
            };
            _extensions = new Dictionary<string, IExtension>
            {
                ["dispatcher"] = new RabbitMqDispatcher()
            };
        }

        public Action<IRouteBuilder> Build()
            => async routeBuilder =>
            {
                foreach (var extension in _extensions)
                {
                    await extension.Value.InitAsync(_configuration);
                }

                foreach (var group in _configuration.Routes)
                {
                    BuildRoutes(routeBuilder, group.Key, group.Value);
                }
            };


        private void BuildRoutes(IRouteBuilder routeBuilder, string group, IEnumerable<Route> routes)
        {
            foreach (var route in routes)
            {
                BuildRoutes(routeBuilder, route);
            }
        }

        private void BuildRoutes(IRouteBuilder routeBuilder, Route route)
        {
            var method = (string.IsNullOrWhiteSpace(route.Method) ? "get" : route.Method).ToLowerInvariant();
            var path = (string.IsNullOrWhiteSpace(route.Upstream) ? "/" : route.Upstream);
            _methods[method](routeBuilder, path, route);
        }

        private Func<HttpRequest, HttpResponse, RouteData, Task> UseDispatcherTypeAsync(Route route)
            => async (request, response, data) =>
            {
                if (!await CanExecuteAsync(request, response, data, route))
                {
                    return;
                }

                var dispatcher = _extensions["dispatcher"];
                var executionData = await _requestProcessor.ProcessAsync(route, request, response, data);
                await dispatcher.ExecuteAsync(executionData);
            };

        private Func<HttpRequest, HttpResponse, RouteData, Task> UseLocalTypeAsync(Route route)
            => async (request, response, data) =>
            {
                if (!await CanExecuteAsync(request, response, data, route))
                {
                    return;
                }

                await response.WriteAsync(route.Return);
            };


        private Func<HttpRequest, HttpResponse, RouteData, Task> UseRequestTypeAsync(Route route)
            => async (request, response, data) =>
            {
                if (!await CanExecuteAsync(request, response, data, route))
                {
                    return;
                }

                if (route.Downstream == null)
                {
                    return;
                }

                var executionData = await _requestProcessor.ProcessAsync(route, request, response, data);
                if (string.IsNullOrWhiteSpace(executionData.Url))
                {
                    return;
                }

                var httpRequest = GetRequest(route, executionData);
                if (httpRequest == null)
                {
                    return;
                }

                var httpResponse = await httpRequest();
                var content = await httpResponse.Content.ReadAsStringAsync();
                await response.WriteAsync(content);
            };

        private async Task<bool> CanExecuteAsync(HttpRequest request, HttpResponse response,
            RouteData data, Route route)
            => await IsAuthorizedAsync(request, response, route);

        private async Task<bool> IsAuthorizedAsync(HttpRequest request, HttpResponse response, Route route)
        {
            if (_configuration.Config.Authentication?.Global != true || (route.Auth.HasValue && route.Auth == false))
            {
                return true;
            }

            await request.HttpContext.AuthenticateAsync();
            if (route.Claims == null || !route.Claims.Any())
            {
                return true;
            }

            var hasClaims = route.Claims.All(claim => request.HttpContext.User.Claims
                .Any(c => c.Type == claim.Key && c.Value == claim.Value));
            if (hasClaims)
            {
                return true;
            }

            response.StatusCode = 401;

            return false;
        }

        private Func<Task<HttpResponseMessage>> GetRequest(Route route, ExecutionData executionData)
        {
            var url = executionData.Url;
            var httpClient = _serviceProvider.GetService<IHttpClientFactory>().CreateClient();
            var payload = GetPayload(executionData.Payload, executionData.ContentType);
            var method = (string.IsNullOrWhiteSpace(route.DownstreamMethod) ? route.Method : route.DownstreamMethod)
                .ToLowerInvariant();
            switch (method)
            {
                case "get":
                    return () => httpClient.GetAsync(url);
                case "post":
                    return () => httpClient.PostAsync(url, payload);
                case "put":
                    return () => httpClient.PutAsJsonAsync(url, payload);
                case "delete":
                    return () => httpClient.DeleteAsync(url);
                case "patch":
                    return () => httpClient.PatchAsync(url, payload);
            }

            return null;
        }

        private static StringContent GetPayload(object data, string contentType)
        {
            if (data == null || string.IsNullOrWhiteSpace(contentType))
            {
                return new StringContent(string.Empty);
            }

            switch (contentType.ToLowerInvariant())
            {
                case "application/json":
                    return new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
            }

            return new StringContent(string.Empty);
        }
    }
}