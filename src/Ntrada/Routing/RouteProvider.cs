using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
        private static readonly Regex VariablesRegex = new Regex(@"\{(.*?)\}", RegexOptions.Compiled);
        private static readonly string[] DefaultExtensions = {"downstream", "return_value"};
        private static readonly string[] AvailableExtensions = {"dispatcher"};
        private static readonly string[] ExcludedResponseHeaders = {"transfer-encoding", "content-length"};
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

            if (!_configuration.Extensions.Any())
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
                    routeInfo =
                        $"call a downstream: [{route.DownstreamMethod.ToUpperInvariant()}] '{route.Downstream}'";
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
                var traceId = request.HttpContext.TraceIdentifier;
                _logger.LogInformation($"Dispatching a message: {routeConfig.Route.RoutingKey} to the exchange: {routeConfig.Route.Exchange} [Trace ID: {traceId}]");
                await dispatcher.ExecuteAsync(executionData);
                response.Headers.Add("Request-ID", executionData.RequestId);
                response.Headers.Add("Resource-ID", executionData.ResourceId);
                response.Headers.Add("Trace-ID", executionData.Request.HttpContext.TraceIdentifier);
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

                if (routeConfig.Route.Downstream is null)
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

                var method = routeConfig.Route.Method.ToUpperInvariant();
                _logger.LogInformation($"Sending HTTP {method} request to: {routeConfig.Downstream} [Trace ID: {request.HttpContext.TraceIdentifier}]");
                var httpResponse = GetRequestAsync(executionData);
                if (httpResponse is null)
                {
                    return;
                }

                await WriteResponseAsync(response, await httpResponse(), executionData);
            };

        private static async Task<bool> IsPayloadValidAsync(ExecutionData executionData, HttpResponse httpResponse)
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
            var traceId = request.HttpContext.TraceIdentifier;
            var isAuthenticated = await _accessValidator.IsAuthenticatedAsync(request, routeConfig);
            if (!isAuthenticated)
            {
                _logger.LogWarning($"Unauthorized request to: {routeConfig.Route.Upstream} [Trace ID: {traceId}]");
                response.StatusCode = 401;

                return false;
            }

            if (!_accessValidator.IsAuthorized(request.HttpContext.User, routeConfig))
            {
                _logger.LogWarning($"Forbidden request to: {routeConfig.Route.Upstream} by user: {request.HttpContext.User.Identity.Name} [Trace ID: {traceId}]");
                response.StatusCode = 403;

                return false;
            }

            return true;
        }

        private Func<Task<HttpResponseMessage>> GetRequestAsync(ExecutionData executionData)
        {
            var url = executionData.Downstream;
            var httpClient = _serviceProvider.GetService<IHttpClientFactory>().CreateClient("ntrada");
            var payload = GetPayload(executionData.Payload, executionData.ContentType);
            var method = (string.IsNullOrWhiteSpace(executionData.Route.DownstreamMethod)
                    ? executionData.Route.Method
                    : executionData.Route.DownstreamMethod)
                .ToLowerInvariant();


            if (executionData.Route.ForwardRequestHeaders == true ||
                (_configuration.ForwardRequestHeaders == true && executionData.Route.ForwardRequestHeaders != false))
            {
                foreach (var header in executionData.Request.Headers)
                {
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            var requestHeaders = executionData.Route.RequestHeaders is null ||
                                   !executionData.Route.RequestHeaders.Any()
                ? _configuration.RequestHeaders ?? new Dictionary<string, string>()
                : executionData.Route.RequestHeaders;
            foreach (var header in requestHeaders)
            {
                if (!string.IsNullOrWhiteSpace(header.Value))
                {
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                    continue;
                }

                if (!executionData.Request.Headers.TryGetValue(header.Key, out var values))
                {
                    continue;
                }

                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, values.ToArray());
            }


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
            if (data is null || string.IsNullOrWhiteSpace(contentType))
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

        private async Task WriteResponseAsync(HttpResponse response, HttpResponseMessage httpResponse,
            ExecutionData executionData)
        {
            var traceId = executionData.Request.HttpContext.TraceIdentifier;
            var method = executionData.Route.Method.ToUpperInvariant();
            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Received an invalid response ({httpResponse.StatusCode}) to HTTP {method} request from: {executionData.Route.Downstream} [Trace ID: {traceId}]");
                await SetErrorResponseAsync(response, httpResponse, executionData);
                return;
            }

            _logger.LogInformation($"Received the successful response ({httpResponse.StatusCode}) to HTTP {method} request from: {executionData.Route.Downstream} [Trace ID: {traceId}]");
            await SetSuccessResponseAsync(response, httpResponse, executionData);
        }

        private async Task SetErrorResponseAsync(HttpResponse response, HttpResponseMessage httpResponse,
            ExecutionData executionData)
        {
            var onError = executionData.Route.OnError;
            var content = await httpResponse.Content.ReadAsStringAsync();
            if (onError is null)
            {
                response.StatusCode = (int) httpResponse.StatusCode;
                await response.WriteAsync(content);
                return;
            }

            response.StatusCode = onError.Code > 0 ? onError.Code : 400;
            await response.WriteAsync(content);
        }

        private async Task SetSuccessResponseAsync(HttpResponse response, HttpResponseMessage httpResponse,
            ExecutionData executionData)
        {
            const string responseDataKey = "response.data";
            var content = await httpResponse.Content.ReadAsStringAsync();
            var onSuccess = executionData.Route.OnSuccess;
            if (_configuration.ForwardStatusCode == false || executionData.Route.ForwardStatusCode == false)
            {
                response.StatusCode = 200;
            }
            else
            {
                response.StatusCode = (int) httpResponse.StatusCode;
            }

            if (executionData.Route.ForwardResponseHeaders == true ||
                (_configuration.ForwardResponseHeaders == true && executionData.Route.ForwardResponseHeaders != false))
            {
                foreach (var header in httpResponse.Headers)
                {
                    if (ExcludedResponseHeaders.Contains(header.Key.ToLowerInvariant()))
                    {
                        continue;
                    }
                    
                    if (response.Headers.ContainsKey(header.Key))
                    {
                        continue;
                    }

                    response.Headers.Add(header.Key, header.Value.ToArray());
                }
            }

            var responseHeaders = executionData.Route.ResponseHeaders is null ||
                                   !executionData.Route.ResponseHeaders.Any()
                ? _configuration.ResponseHeaders ?? new Dictionary<string, string>()
                : executionData.Route.ResponseHeaders;
            foreach (var header in responseHeaders)
            {
                if (string.IsNullOrWhiteSpace(header.Value))
                {
                    continue;
                }

                if (header.Value.Contains("^"))
                {
                    var modifiers = header.Value.Split('^');
                    var basePart = modifiers[0];
                    var modifier = modifiers[1];
                    if (string.IsNullOrWhiteSpace(modifier) || !modifier.StartsWith("response:headers:"))
                    {
                        continue;
                    }

                    var parts = modifier.Split(':');
                    if (parts.Length != 4)
                    {
                        continue;
                    }

                    var headerName = parts[2];
                    var value = parts[3];
                    if (!httpResponse.Headers.TryGetValues(headerName, out var headerValues))
                    {
                        continue;
                    }

                    var headerValue = headerValues?.FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(headerValue))
                    {
                        continue;
                    }

                    var baseMatches = VariablesRegex.Match(basePart);
                    var valueMatches = VariablesRegex.Match(value);
                    if (!baseMatches.Success || !valueMatches.Success || baseMatches.Value != valueMatches.Value)
                    {
                        continue;
                    }

                    var plainValue = value.Replace(valueMatches.Value, string.Empty);
                    var headerPlainValue = headerValue.Replace(plainValue, string.Empty);
                    var newHeaderValue = basePart.Replace(baseMatches.Value, headerPlainValue);
                    if (string.IsNullOrWhiteSpace(newHeaderValue))
                    {
                        continue;
                    }
                    
                    response.Headers.Remove(header.Key);
                    response.Headers.Add(header.Key, newHeaderValue);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(header.Value) && !header.Value.Contains("^"))
                {
                    response.Headers.Remove(header.Key);
                    response.Headers.Add(header.Key, header.Value);
                    continue;
                }
                
                if (!httpResponse.Headers.TryGetValues(header.Key, out var values))
                {
                    continue;
                }

                response.Headers.Remove(header.Key);
                response.Headers.Add(header.Key, values.ToArray());
            }

            if (onSuccess is null)
            {
                if (response.StatusCode != 204)
                {
                    await response.WriteAsync(content);
                }
                
                return;
            }
            
            response.StatusCode = onSuccess.Code > 0 ? onSuccess.Code : response.StatusCode;
            if (response.StatusCode == 204)
            {
                return;
            }
            
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

            if (!(onSuccess.Data is null))
            {
                await response.WriteAsync(onSuccess.Data.ToString());
            }
        }
    }
}