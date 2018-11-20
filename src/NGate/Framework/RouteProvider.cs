using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

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
            _extensions = LoadExtensions();
        }

        private IDictionary<string, IExtension> LoadExtensions()
        {
            var extensions = new Dictionary<string, IExtension>();

            if (_configuration.Extensions == null)
            {
                return extensions;
            }

            var extensionsTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.GetInterfaces().Contains(typeof(IExtension)))
                .ToList();

            if (!extensionsTypes.Any())
            {
                return extensions;
            }

            var loadedExtensions = new Dictionary<string, IExtension>();
            foreach (var extensionType in extensionsTypes)
            {
                var extension = Activator.CreateInstance(extensionType, _configuration) as IExtension;
                if (extension == null)
                {
                    continue;
                }

                loadedExtensions[extension.Name] = extension;
            }

            foreach (var extension in _configuration.Extensions)
            {
                var extensionName = extension.Value.Use;
                if (!loadedExtensions.ContainsKey(extensionName))
                {
                    throw new ArgumentException($"Extension: '{extensionName}' was not found.", nameof(extensionName));
                }

                extensions[extension.Key] = loadedExtensions[extensionName];
            }

            return extensions;
        }

        public Action<IRouteBuilder> Build()
            => async routeBuilder =>
            {
                foreach (var extension in _extensions)
                {
                    await extension.Value.InitAsync();
                }

                foreach (var module in _configuration.Modules.Where(m => m.Enabled != false))
                {
                    BuildRoutes(routeBuilder, module);
                }
            };


        private void BuildRoutes(IRouteBuilder routeBuilder, Module module)
        {
            foreach (var route in module.Routes)
            {
                BuildRoutes(routeBuilder, module, route);
            }
        }

        private void BuildRoutes(IRouteBuilder routeBuilder, Module module, Route route)
        {
            var upstream = string.IsNullOrWhiteSpace(route.Upstream) ? string.Empty : route.Upstream;
            if (!string.IsNullOrWhiteSpace(module.Path))
            {
                var modulePath = module.Path.EndsWith("/") ? module.Path : $"{module.Path}/";
                if (upstream.StartsWith("/"))
                {
                    upstream = upstream.Substring(1, upstream.Length - 1);
                }

                if (upstream.EndsWith("/"))
                {
                    upstream = upstream.Substring(0, upstream.Length - 1);
                }

                upstream = $"{modulePath}{upstream}";
            }

            if (string.IsNullOrWhiteSpace(upstream))
            {
                upstream = "/";
            }

            route.Upstream = upstream;
            var routeConfig = _routeConfigurator.Configure(module, route);
            _methods[route.Method](routeBuilder, route.Upstream, routeConfig);
        }

        private Func<HttpRequest, HttpResponse, RouteData, Task> UseDispatcherAsync(RouteConfig routeConfig)
            => async (request, response, data) =>
            {
                if (!await CanExecuteAsync(request, response, data, routeConfig))
                {
                    return;
                }

                const string name = "dispatcher";
                if (!_extensions.ContainsKey(name))
                {
                    throw new InvalidOperationException($"Extension for: '{name}' was not found.");
                }

                var executionData = await _requestProcessor.ProcessAsync(routeConfig, request, response, data);
                if (!await IsPayloadValidAsync(executionData, response))
                {
                    return;
                }

                var dispatcher = _extensions[name];
                await dispatcher.ExecuteAsync(executionData);
                response.Headers.Add("X-Operation", executionData.RequestId);
                response.Headers.Add("X-Resource", executionData.ResourceId);
                response.StatusCode = 202;
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
                if (!await IsPayloadValidAsync(executionData, response))
                {
                    return;
                }

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
                if (!httpResponse.IsSuccessStatusCode)
                {
                    response.StatusCode = (int) httpResponse.StatusCode;
                    return;
                }

                var content = await httpResponse.Content.ReadAsStringAsync();
                await response.WriteAsync(content);
            };

        private async Task<bool> IsPayloadValidAsync(ExecutionData executionData, HttpResponse httpResponse)
        {
            if (executionData.IsPayloadValid)
            {
                return true;
            }

            var response = new {errors = executionData.ValidationErrors};
            var payload = JsonConvert.SerializeObject(response);
            httpResponse.ContentType = "application/json";
            await httpResponse.WriteAsync(payload);

            return false;
        }

        private async Task<bool> CanExecuteAsync(HttpRequest request, HttpResponse response,
            RouteData data, RouteConfig routeConfig)
            => await IsAuthorizedAsync(request, response, routeConfig);

        private async Task<bool> IsAuthorizedAsync(HttpRequest request, HttpResponse response, RouteConfig routeConfig)
        {
            if (_configuration.Auth?.Global != true
                || (routeConfig.Route.Auth.HasValue && routeConfig.Route.Auth == false))
            {
                return true;
            }

            var result = await request.HttpContext.AuthenticateAsync();
            if (!result.Succeeded)
            {
                response.StatusCode = 401;

                return false;
            }

            if (routeConfig.Route.Claims == null || !routeConfig.Route.Claims.Any())
            {
                return true;
            }

            var hasClaims = routeConfig.Claims.All(claim => request.HttpContext.User.Claims
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
            var httpClient = _serviceProvider.GetService<IHttpClientFactory>().CreateClient("ngate");
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