using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NGate.Extensions.RabbitMq;

namespace NGate.Framework
{
    public class RouteProvider
    {
        private readonly IDictionary<string, Action<IRouteBuilder, string, RouteConfig>> _methods;
        private readonly IDictionary<string, IExtension> _extensions;
        private readonly IServiceProvider _serviceProvider;
        private readonly IRequestProcessor _requestProcessor;
        private readonly IRouteConfigurator _routeConfigurator;
        private readonly Configuration _configuration;

        public RouteProvider(IServiceProvider serviceProvider, IRequestProcessor requestProcessor,
            IRouteConfigurator routeConfigurator, Configuration configuration)
        {
            _serviceProvider = serviceProvider;
            _requestProcessor = requestProcessor;
            _routeConfigurator = routeConfigurator;
            _configuration = configuration;
            var processors = new Dictionary<string, Func<RouteConfig, Func<HttpRequest, HttpResponse, RouteData, Task>>>
            {
                ["return_value"] = UseReturnValueAsync,
                ["downstream"] = UseDownstreamAsync,
                ["dispatcher"] = UseDispatcherAsync
            };
            _methods = new Dictionary<string, Action<IRouteBuilder, string, RouteConfig>>
            {
                ["get"] = (builder, path, routeConfig) =>
                    builder.MapGet(path, processors[routeConfig.Route.Use](routeConfig)),
                ["post"] = (builder, path, routeConfig) =>
                    builder.MapPost(path, processors[routeConfig.Route.Use](routeConfig)),
                ["put"] = (builder, path, routeConfig) =>
                    builder.MapPut(path, processors[routeConfig.Route.Use](routeConfig)),
                ["delete"] = (builder, path, routeConfig) =>
                    builder.MapDelete(path, processors[routeConfig.Route.Use](routeConfig)),
                ["patch"] = (builder, path, routeConfig) =>
                    builder.MapVerb("patch", path, processors[routeConfig.Route.Use](routeConfig))
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
            route.Method = (string.IsNullOrWhiteSpace(route.Method) ? "get" : route.Method).ToLowerInvariant();;
            route.Upstream = (string.IsNullOrWhiteSpace(route.Upstream) ? "/" : route.Upstream);
            var routeConfig = _routeConfigurator.Configure(route);
            _methods[route.Method](routeBuilder, route.Upstream, routeConfig);
        }

        private Func<HttpRequest, HttpResponse, RouteData, Task> UseDispatcherAsync(RouteConfig routeConfig)
            => async (request, response, data) =>
            {
                if (!await CanExecuteAsync(request, response, data, routeConfig))
                {
                    return;
                }

                var dispatcher = _extensions["dispatcher"];
                var executionData = await _requestProcessor.ProcessAsync(routeConfig, request, response, data);
                await dispatcher.ExecuteAsync(executionData);
            };

        private Func<HttpRequest, HttpResponse, RouteData, Task> UseReturnValueAsync(RouteConfig routeConfig)
            => async (request, response, data) =>
            {
                if (!await CanExecuteAsync(request, response, data, routeConfig))
                {
                    return;
                }

                await response.WriteAsync(routeConfig.Route.ReturnValue);
            };


        private Func<HttpRequest, HttpResponse, RouteData, Task> UseDownstreamAsync(RouteConfig routeConfig)
            => async (request, response, data) =>
            {
                if (!await CanExecuteAsync(request, response, data, routeConfig))
                {
                    return;
                }

                if (routeConfig.Route.Downstream == null)
                {
                    return;
                }

                var executionData = await _requestProcessor.ProcessAsync(routeConfig, request, response, data);
                if (string.IsNullOrWhiteSpace(executionData.Downstream))
                {
                    return;
                }

                var httpRequest = GetRequest(executionData);
                if (httpRequest == null)
                {
                    return;
                }

                var httpResponse = await httpRequest();
                var content = await httpResponse.Content.ReadAsStringAsync();
                await response.WriteAsync(content);
            };

        private async Task<bool> CanExecuteAsync(HttpRequest request, HttpResponse response,
            RouteData data, RouteConfig routeConfig)
            => await IsAuthorizedAsync(request, response, routeConfig);

        private async Task<bool> IsAuthorizedAsync(HttpRequest request, HttpResponse response, RouteConfig routeConfig)
        {
            if (_configuration.Config.Authentication?.Global != true
                || (routeConfig.Route.Auth.HasValue && routeConfig.Route.Auth == false))
            {
                return true;
            }

            var result = await request.HttpContext.AuthenticateAsync();
            if (!result.Succeeded)
            {
                return false;
            }

            if (routeConfig.Route.Claims == null || !routeConfig.Route.Claims.Any())
            {
                return true;
            }

            var hasClaims = routeConfig.Route.Claims.All(claim => request.HttpContext.User.Claims
                .Any(c => c.Type == claim.Key && c.Value == claim.Value));
            if (hasClaims)
            {
                return true;
            }

            response.StatusCode = 401;

            return false;
        }

        private Func<Task<HttpResponseMessage>> GetRequest(ExecutionData executionData)
        {
            var url = executionData.Downstream;
            var httpClient = _serviceProvider.GetService<IHttpClientFactory>().CreateClient();
            var payload = GetPayload(executionData.Payload, executionData.ContentType);
            var method = (string.IsNullOrWhiteSpace(executionData.Route.DownstreamMethod)
                    ? executionData.Route.Method
                    : executionData.Route.DownstreamMethod)
                .ToLowerInvariant();
            switch (method)
            {
                case "get":
                    return () => httpClient.GetAsync(url);
                case "post":
                    return () => httpClient.PostAsync(url, payload);
                case "put":
                    return () => httpClient.PutAsync(url, payload);
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