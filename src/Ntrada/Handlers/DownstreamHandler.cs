using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ntrada.Hooks;
using Ntrada.Options;
using Route = Ntrada.Configuration.Route;

namespace Ntrada.Handlers
{
    internal sealed class DownstreamHandler : IHandler
    {
        private const string ContentTypeApplicationJson = "application/json";
        private const string ContentTypeHeader = "Content-Type";
        private static readonly string[] ExcludedResponseHeaders = {"transfer-encoding", "content-length"};

        private static readonly HttpContent EmptyContent =
            new StringContent("{}", Encoding.UTF8, ContentTypeApplicationJson);
        private readonly IRequestProcessor _requestProcessor;
        private readonly IPayloadValidator _payloadValidator;
        private readonly NtradaOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DownstreamHandler> _logger;
        private readonly IEnumerable<IRequestHook> _requestHooks;
        private readonly IEnumerable<IResponseHook> _responseHooks;
        private readonly IEnumerable<IHttpRequestHook> _httpRequestHooks;
        private readonly IEnumerable<IHttpResponseHook> _httpResponseHooks;

        public DownstreamHandler(IServiceProvider serviceProvider, IRequestProcessor requestProcessor,
            IPayloadValidator payloadValidator, NtradaOptions options, IHttpClientFactory httpClientFactory,
            ILogger<DownstreamHandler> logger)
        {
            _requestProcessor = requestProcessor;
            _payloadValidator = payloadValidator;
            _options = options;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _requestHooks = serviceProvider.GetServices<IRequestHook>();
            _responseHooks = serviceProvider.GetServices<IResponseHook>();
            _httpRequestHooks = serviceProvider.GetServices<IHttpRequestHook>();
            _httpResponseHooks = serviceProvider.GetServices<IHttpResponseHook>();
        }

        public string GetInfo(Route route) => $"call the downstream: '{route.Downstream}'";

        public async Task HandleAsync(HttpContext context, RouteConfig config)
        {
            if (config.Route.Downstream is null)
            {
                return;
            }

            var executionData = await _requestProcessor.ProcessAsync(config, context);
            if (_requestHooks is {})
            {
                foreach (var hook in _requestHooks)
                {
                    if (hook is null)
                    {
                        continue;
                    }

                    await hook.InvokeAsync(context.Request, executionData);
                }
            }
            
            if (!executionData.IsPayloadValid)
            {
                await _payloadValidator.TryValidate(executionData, context.Response);
                return;
            }

            if (string.IsNullOrWhiteSpace(executionData.Downstream))
            {
                return;
            }
            
            _logger.LogInformation($"Sending HTTP {context.Request.Method} request to: {config.Downstream} " +
                                   $"[Trace ID: {context.TraceIdentifier}]");
            
            var response = await SendRequestAsync(executionData);
            if (response is null)
            {
                _logger.LogWarning($"Did not receive HTTP response for: {executionData.Route.Downstream}");

                return;
            }
            
            await WriteResponseAsync(context.Response, response, executionData);
        }

        private async Task<HttpResponseMessage> SendRequestAsync(ExecutionData executionData)
        {
            var httpClient = _httpClientFactory.CreateClient("ntrada");
            var method = (string.IsNullOrWhiteSpace(executionData.Route.DownstreamMethod)
                ? executionData.Context.Request.Method
                : executionData.Route.DownstreamMethod).ToLowerInvariant();

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(executionData.Downstream)
            };

            if (executionData.Route.ForwardRequestHeaders == true ||
                _options.ForwardRequestHeaders == true && executionData.Route.ForwardRequestHeaders != false)
            {
                foreach (var (key, value) in executionData.Context.Request.Headers)
                {
                    request.Headers.TryAddWithoutValidation(key, value.ToArray());
                }
            }

            var requestHeaders = executionData.Route.RequestHeaders is null ||
                                 !executionData.Route.RequestHeaders.Any()
                ? _options.RequestHeaders
                : executionData.Route.RequestHeaders;

            if (requestHeaders is {})
            {
                foreach (var (key, value) in requestHeaders)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        request.Headers.TryAddWithoutValidation(key, value);
                        continue;
                    }

                    if (!executionData.Context.Request.Headers.TryGetValue(key, out var values))
                    {
                        continue;
                    }

                    request.Headers.TryAddWithoutValidation(key, values.ToArray());
                }
            }

            var includeBody = false;
            switch (method)
            {
                case "get":
                    request.Method = HttpMethod.Get;
                    break;
                case "post":
                    request.Method = HttpMethod.Post;
                    includeBody = true;
                    break;
                case "put":
                    request.Method = HttpMethod.Put;
                    includeBody = true;
                    break;
                case "patch":
                    request.Method = HttpMethod.Patch;
                    includeBody = true;
                    break;
                case "delete":
                    request.Method = HttpMethod.Delete;
                    break;
                case "head":
                    request.Method = HttpMethod.Head;
                    break;
                case "options":
                    request.Method = HttpMethod.Options;
                    break;
                case "trace":
                    request.Method = HttpMethod.Trace;
                    break;
                default:
                    return null;
            }
            
            if (_httpRequestHooks is {})
            {
                foreach (var hook in _httpRequestHooks)
                {
                    if (hook is null)
                    {
                        continue;
                    }

                    await hook.InvokeAsync(request, executionData);
                }
            }
            
            if (!includeBody)
            {
                return await httpClient.SendAsync(request);
            }

            using var content = GetHttpContent(executionData);
            request.Content = content;
            return await httpClient.SendAsync(request);
        }

        private static HttpContent GetHttpContent(ExecutionData executionData)
        {
            var data = executionData.Payload;
            var contentType = executionData.ContentType;
            if (executionData.HasPayload)
            {
                if (data is null || !contentType.StartsWith(ContentTypeApplicationJson))
                {
                    return EmptyContent;
                }

                return new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, ContentTypeApplicationJson);
            }
            
            if (executionData.Context.Request.Body is null)
            {
                return EmptyContent;
            }

            var httpContent = new StreamContent(executionData.Context.Request.Body);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            return httpContent;
        }

        private async Task WriteResponseAsync(HttpResponse response, HttpResponseMessage httpResponse,
            ExecutionData executionData)
        {
            var traceId = executionData.Context.Request.HttpContext.TraceIdentifier;
            var method = executionData.Context.Request.Method;
            if (!string.IsNullOrWhiteSpace(executionData.RequestId))
            {
                response.Headers.Add("Request-ID", executionData.RequestId);
            }

            if (!string.IsNullOrWhiteSpace(executionData.ResourceId) && executionData.Context.Request.Method is "POST")
            {
                response.Headers.Add("Resource-ID", executionData.ResourceId);
            }

            if (!string.IsNullOrWhiteSpace(executionData.TraceId))
            {
                response.Headers.Add("Trace-ID", executionData.TraceId);
            }
            
            if (_httpResponseHooks is {})
            {
                foreach (var hook in _httpResponseHooks)
                {
                    if (hook is null)
                    {
                        continue;
                    }

                    await hook.InvokeAsync(httpResponse, executionData);
                }
            }
            
            if (_responseHooks is {})
            {
                foreach (var hook in _responseHooks)
                {
                    if (hook is null)
                    {
                        continue;
                    }

                    await hook.InvokeAsync(response, executionData);
                }
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
            if (executionData.Context.Request.Method is "GET" && !response.Headers.ContainsKey(ContentTypeHeader))
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
            if (_options.ForwardStatusCode == false || executionData.Route.ForwardStatusCode == false)
            {
                response.StatusCode = 200;
            }
            else
            {
                response.StatusCode = (int) httpResponse.StatusCode;
            }

            if (executionData.Route.ForwardResponseHeaders == true ||
                (_options.ForwardResponseHeaders == true && executionData.Route.ForwardResponseHeaders != false))
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
                ? _options.ResponseHeaders ?? new Dictionary<string, string>()
                : executionData.Route.ResponseHeaders;
            foreach (var header in responseHeaders)
            {
                if (string.IsNullOrWhiteSpace(header.Value))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(header.Value))
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

            if (executionData.Context.Request.Method is "GET" && !response.Headers.ContainsKey(ContentTypeHeader))
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

            if (onSuccess.Data is {})
            {
                await response.WriteAsync(onSuccess.Data.ToString());
            }
        }
    }
}