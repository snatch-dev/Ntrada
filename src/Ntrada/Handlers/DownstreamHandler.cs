using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ntrada.Core;
using Ntrada.Hooks;
using Route = Ntrada.Core.Configuration.Route;

namespace Ntrada.Handlers
{
    public class DownstreamHandler : IHandler
    {
        private static readonly string TransformationToken = "^";
        private static readonly string ContentTypeApplicationJson = "application/json";
        private static readonly string ContentTypeHeader = "Content-Type";
        private static readonly Regex VariablesRegex = new Regex(@"\{(.*?)\}", RegexOptions.Compiled);
        private static readonly string[] ExcludedResponseHeaders = {"transfer-encoding", "content-length"};
        private readonly IServiceProvider _serviceProvider;
        private readonly IRequestProcessor _requestProcessor;
        private readonly IPayloadValidator _payloadValidator;
        private readonly NtradaConfiguration _configuration;
        private readonly ILogger<DownstreamHandler> _logger;
        private readonly IBeforeHttpClientRequestHook _beforeHttpClientRequestHook;

        public DownstreamHandler(IServiceProvider serviceProvider, IRequestProcessor requestProcessor,
            IPayloadValidator payloadValidator, NtradaConfiguration configuration, ILogger<DownstreamHandler> logger)
        {
            _serviceProvider = serviceProvider;
            _requestProcessor = requestProcessor;
            _payloadValidator = payloadValidator;
            _configuration = configuration;
            _logger = logger;
            _beforeHttpClientRequestHook = _serviceProvider.GetService<IBeforeHttpClientRequestHook>();
        }

        public string GetInfo(Route route) =>
            $"call the downstream: [{route.DownstreamMethod.ToUpperInvariant()}] '{route.Downstream}'";

        public async Task HandleAsync(HttpRequest request, HttpResponse response, RouteData routeData,
            RouteConfig routeConfig)
        {
            if (routeConfig.Route.Downstream is null)
            {
                return;
            }

            var executionData = await _requestProcessor.ProcessAsync(routeConfig, request, response, routeData);
            if (!await _payloadValidator.TryValidate(executionData, response))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(executionData.Downstream))
            {
                return;
            }

            var method = routeConfig.Route.Method.ToUpperInvariant();
            _logger.LogInformation($"Sending HTTP {method} request to: {routeConfig.Downstream} " +
                                   $"[Trace ID: {request.HttpContext.TraceIdentifier}]");
            var httpResponse = SendRequestAsync(executionData);
            if (httpResponse is null)
            {
                return;
            }

            await WriteResponseAsync(response, await httpResponse(), executionData);
        }

        private Func<Task<HttpResponseMessage>> SendRequestAsync(ExecutionData executionData)
        {
            var httpClient = _serviceProvider.GetService<IHttpClientFactory>().CreateClient("ntrada");
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

            _beforeHttpClientRequestHook?.Invoke(httpClient, executionData);

            switch (method)
            {
                case "get":
                    return () =>
                    {
                        var url = executionData.Downstream;
                        return httpClient.GetAsync(url);
                    };
                case "post":
                    return () =>
                    {
                        var url = executionData.Downstream;
                        var payload = GetPayload(executionData.Payload, executionData.ContentType);
                        return httpClient.PostAsync(url, payload);
                    };
                case "put":
                    return () =>
                    {
                        var url = executionData.Downstream;
                        var payload = GetPayload(executionData.Payload, executionData.ContentType);
                        return httpClient.PutAsync(url, payload);
                    };
                case "delete":
                    return () =>
                    {
                        var url = executionData.Downstream;
                        return httpClient.DeleteAsync(url);
                    };
                default:
                    return null;
            }
        }

        private static StringContent GetPayload(object data, string contentType)
        {
            if (data is null || string.IsNullOrWhiteSpace(contentType))
            {
                return new StringContent(string.Empty);
            }

            return contentType.StartsWith(ContentTypeApplicationJson)
                ? new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, ContentTypeApplicationJson)
                : new StringContent(string.Empty);
        }

        private async Task WriteResponseAsync(HttpResponse response, HttpResponseMessage httpResponse,
            ExecutionData executionData)
        {
            var traceId = executionData.Request.HttpContext.TraceIdentifier;
            var method = executionData.Route.Method.ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(executionData.RequestId))
            {
                response.Headers.Add("Request-ID", executionData.RequestId);
            }

            if (!string.IsNullOrWhiteSpace(executionData.ResourceId) && executionData.Route.Method == "post")
            {
                response.Headers.Add("Resource-ID", executionData.ResourceId);
            }

            if (!string.IsNullOrWhiteSpace(executionData.TraceId))
            {
                response.Headers.Add("Trace-ID", executionData.TraceId);
            }

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Received an invalid response ({httpResponse.StatusCode}) to HTTP " +
                                       $"{method} request from: {executionData.Route.Downstream} [Trace ID: {traceId}]");
                await SetErrorResponseAsync(response, httpResponse, executionData);
                return;
            }

            _logger.LogInformation($"Received the successful response ({httpResponse.StatusCode}) to HTTP " +
                                   $"{method} request from:{executionData.Route.Downstream} [Trace ID: {traceId}]");
            await SetSuccessResponseAsync(response, httpResponse, executionData);
        }

        private static async Task SetErrorResponseAsync(HttpResponse response, HttpResponseMessage httpResponse,
            ExecutionData executionData)
        {
            var onError = executionData.Route.OnError;
            var content = await httpResponse.Content.ReadAsStringAsync();
            if (executionData.Route.Method == "get" && !response.Headers.ContainsKey(ContentTypeHeader))
            {
                response.Headers[ContentTypeHeader] = ContentTypeApplicationJson;
            }

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

                if (header.Value.Contains(TransformationToken))
                {
                    HandleTransformation(header, response, httpResponse, executionData);
                }

                if (!string.IsNullOrWhiteSpace(header.Value) && !header.Value.Contains(TransformationToken))
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

            if (executionData.Route.Method == "get" && !response.Headers.ContainsKey(ContentTypeHeader))
            {
                response.Headers[ContentTypeHeader] = ContentTypeApplicationJson;
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

        private static void HandleTransformation(KeyValuePair<string, string> header, HttpResponse response,
            HttpResponseMessage httpResponse, ExecutionData executionData)
        {
            var modifiers = header.Value.Split('^');
            var basePart = modifiers[0];
            var modifier = modifiers[1];
            if (string.IsNullOrWhiteSpace(modifier) || !modifier.StartsWith("response:headers:"))
            {
                return;
            }

            var parts = modifier.Split(':');
            if (parts.Length != 4)
            {
                return;
            }

            var headerName = parts[2];
            var value = parts[3];
            if (!httpResponse.Headers.TryGetValues(headerName, out var headerValues))
            {
                return;
            }

            var headerValue = headerValues?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                return;
            }

            var baseMatches = VariablesRegex.Match(basePart);
            var valueMatches = VariablesRegex.Match(value);
            if (!baseMatches.Success || !valueMatches.Success || baseMatches.Value != valueMatches.Value)
            {
                return;
            }

            var plainValue = value.Replace(valueMatches.Value, string.Empty);
            var headerPlainValue = headerValue.Replace(plainValue, string.Empty);
            var newHeaderValue = basePart.Replace(baseMatches.Value, headerPlainValue);
            if (string.IsNullOrWhiteSpace(newHeaderValue))
            {
                return;
            }

            response.Headers.Remove(header.Key);
            response.Headers.Add(header.Key, newHeaderValue);
        }
    }
}