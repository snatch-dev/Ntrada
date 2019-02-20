using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ntrada.Auth;
using Ntrada.Configuration;
using Ntrada.Extensions;
using Ntrada.Models;
using Ntrada.Requests;

namespace Ntrada.Routing
{
    public class RouteProvider
    {
        private static readonly string[] DefaultExtensions = {"downstream", "return_value"};
        private static readonly string[] AvailableExtensions = {"dispatcher"};
        private readonly IDictionary<string, Action<IRouteBuilder, string, RouteConfig>> _methods;
        private readonly IDictionary<string, IExtension> _extensions;
        private readonly IServiceProvider _serviceProvider;
        private readonly IRequestProcessor _requestProcessor;
        private readonly IRouteConfigurator _routeConfigurator;
        private readonly IAccessValidator _accessValidator;
        private readonly IExtensionManager _extensionManager;
        private readonly NtradaConfiguration _configuration;
        private readonly ILogger<RouteProvider> _logger;

        public RouteProvider(IServiceProvider serviceProvider, IRequestProcessor requestProcessor,
            IRouteConfigurator routeConfigurator, IAccessValidator accessValidator, IExtensionManager extensionManager,
            NtradaConfiguration configuration, ILogger<RouteProvider> logger)
        {
            _serviceProvider = serviceProvider;
            _requestProcessor = requestProcessor;
            _routeConfigurator = routeConfigurator;
            _accessValidator = accessValidator;
            _extensionManager = extensionManager;
            _configuration = configuration;
            _logger = logger;
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
            var usedExtensions = _configuration.Modules
                .SelectMany(m => m.Routes)
                .Select(r => r.Use)
                .Distinct()
                .Except(DefaultExtensions)
                .ToArray();

            var unavailableExtensions = usedExtensions.Except(AvailableExtensions).ToArray();
            if (unavailableExtensions.Any())
            {
                throw new Exception($"Unavailable extensions: '{string.Join(", ", unavailableExtensions)}'");
            }

            var enabledExtensions = _configuration.Extensions.Select(e => e.Key);
            var undefinedExtensions = usedExtensions.Except(enabledExtensions).ToArray();
            if (undefinedExtensions.Any())
            {
                throw new Exception($"Undefined extensions: '{string.Join(", ", undefinedExtensions)}'");
            }

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

            var loadedExtensions = _extensionManager.GetAll().ToDictionary(e => e.Name, e => e);
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
                    _logger.LogInformation($"Building routes for module: '{module.Name}'");
                    foreach (var route in module.Routes)
                    {
                        BuildRoutes(routeBuilder, module, route);
                    }
                }
            };

        private void BuildRoutes(IRouteBuilder routeBuilder, Configuration.Module module, Configuration.Route route)
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

            var routeInfo = string.Empty;
            switch (route.Use)
            {
                case "dispatcher":
                    routeInfo = $"dispatch a message to exchange: '{route.Exchange}'";
                    break;
                case "downstream":
                    routeInfo = $"call a downstream: [{route.DownstreamMethod.ToUpperInvariant()}] '{route.Downstream}'";
                    break;
                case "return_value":
                    routeInfo = $"return a value: '{route.ReturnValue}'";
                    break;
            }

            var isProtectedInfo = _configuration.Auth is null || !_configuration.Auth.Global && route.Auth is null ||
                                  route.Auth == false
                ? "public"
                : "protected";
            _logger.LogInformation($"Added {isProtectedInfo} route for upstream: [{route.Method.ToUpperInvariant()}] '{upstream}'" +
                                   $" -> {routeInfo}");
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

                await WriteResponseAsync(response, await httpRequest(), executionData);
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
        {
            var isAuthenticated = await _accessValidator.IsAuthenticatedAsync(request, routeConfig);
            if (!isAuthenticated)
            {
                response.StatusCode = 401;

                return false;
            }

            if (!_accessValidator.IsAuthorized(request.HttpContext.User, routeConfig))
            {
                response.StatusCode = 403;

                return false;
            }

            return true;
        }

        private Func<Task<HttpResponseMessage>> GetRequest(ExecutionData executionData)
        {
            var url = executionData.Downstream;
            var httpClient = _serviceProvider.GetService<IHttpClientFactory>().CreateClient("ntrada");
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

        private static async Task WriteResponseAsync(HttpResponse response, HttpResponseMessage httpResponse,
            ExecutionData executionData)
        {
            if (!httpResponse.IsSuccessStatusCode)
            {
                SetErrorResponse(response, httpResponse, executionData);
                return;
            }

            await SetSuccessResponseAsync(response, httpResponse, executionData);
        }

        private static void SetErrorResponse(HttpResponse response, HttpResponseMessage httpResponse,
            ExecutionData executionData)
        {
            var onError = executionData.Route.OnError;
            if (onError == null)
            {
                response.StatusCode = (int) httpResponse.StatusCode;

                return;
            }

            response.StatusCode = onError.Code > 0 ? onError.Code : 400;
        }

        private static async Task SetSuccessResponseAsync(HttpResponse response, HttpResponseMessage httpResponse,
            ExecutionData executionData)
        {
            const string responseDataKey = "response.data";
            var content = await httpResponse.Content.ReadAsStringAsync();
            var onSuccess = executionData.Route.OnSuccess;
            if (onSuccess == null)
            {
                await response.WriteAsync(content);
                return;
            }

            response.StatusCode = onSuccess.Code > 0 ? onSuccess.Code : 200;
            if (onSuccess.Data is string dataText && dataText.StartsWith(responseDataKey))
            {
                var dataKey = dataText.Replace(responseDataKey, string.Empty);
                if (string.IsNullOrWhiteSpace(dataKey))
                {
                    await response.WriteAsync(content);
                    return;
                }

                dataKey = dataKey.Substring(1, dataKey.Length - 1);
                dynamic data = new ExpandoObject();
                JsonConvert.PopulateObject(content, data);
                var dictionary = (IDictionary<string, object>) data;
                if (!dictionary.TryGetValue(dataKey, out var dataValue))
                {
                    return;
                }

                switch (dataValue)
                {
                    case JObject jObject:
                        await response.WriteAsync(jObject.ToString());
                        return;
                    case JArray jArray:
                        await response.WriteAsync(jArray.ToString());
                        return;
                    default:
                        await response.WriteAsync(dataValue.ToString());
                        break;
                }
            }

            if (onSuccess.Data != null)
            {
                await response.WriteAsync(onSuccess.Data.ToString());
            }
        }
    }
}