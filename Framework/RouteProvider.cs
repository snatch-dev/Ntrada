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
            var path = (string.IsNullOrWhiteSpace(route.Path) ? "/" : route.Path);
            _methods[method](routeBuilder, path, route);
        }

        private Func<HttpRequest, HttpResponse, RouteData, Task> UseDispatcherTypeAsync(Route route)
            => async (request, response, data) =>
            {
                if (_configuration.Config.Authentication?.Global == true && !route.Auth == false)
                {
                    await request.HttpContext.AuthenticateAsync();
                    foreach (var claim in route.Claims ?? Enumerable.Empty<string>())
                    {
                        if (request.HttpContext.User.Claims.All(c => c.Type != claim))
                        {
                            response.StatusCode = 401;
                            return;
                        }
                    }
                }
                var dispatcher = _extensions["dispatcher"];
                var executionData = await _requestProcessor.ProcessAsync(route, request, response, data);
                await dispatcher.ExecuteAsync(executionData);
            };

        private Func<HttpRequest, HttpResponse, RouteData, Task> UseLocalTypeAsync(Route route)
            => async (request, response, data) =>
            {
                if (_configuration.Config.Authentication?.Global == true && !route.Auth == false)
                {
                    await request.HttpContext.AuthenticateAsync();
                    foreach (var claim in route.Claims ?? Enumerable.Empty<string>())
                    {
                        if (request.HttpContext.User.Claims.All(c => c.Type != claim))
                        {
                            response.StatusCode = 401;
                            return;
                        }
                    }
                }
                await response.WriteAsync(route.Return);
            };

        private Func<HttpRequest, HttpResponse, RouteData, Task> UseRequestTypeAsync(Route route)
            => async (request, response, data) =>
            {
                if (_configuration.Config.Authentication?.Global == true && !route.Auth == false)
                {
                    await request.HttpContext.AuthenticateAsync();
                    foreach (var claim in route.Claims ?? Enumerable.Empty<string>())
                    {
                        if (request.HttpContext.User.Claims.All(c => c.Type != claim))
                        {
                            response.StatusCode = 401;
                            return;
                        }
                    }
                }
                if (route.Upstream == null)
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

        private Func<Task<HttpResponseMessage>> GetRequest(Route route, ExecutionData executionData)
        {
            var url = executionData.Url;
            var httpClient = _serviceProvider.GetService<IHttpClientFactory>().CreateClient();
            var payload = GetPayload(executionData.Payload, executionData.ContentType);
            var method = (string.IsNullOrWhiteSpace(route.UpstreamMethod) ? route.Method : route.UpstreamMethod)
                .ToLowerInvariant();
            switch (method)
            {
                case "get":
                    return () => httpClient.GetAsync(url);
                    break;
                case "post":
                    return () => httpClient.PostAsync(url, payload);
                    break;
                case "put":
                    return () => httpClient.PutAsJsonAsync(url, payload);
                    break;
                case "delete":
                    return () => httpClient.DeleteAsync(url);
                    break;
                case "patch":
                    return () => httpClient.PatchAsync(url, payload);
                    break;
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