using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ntrada.Requests
{
    public class RequestProcessor : IRequestProcessor
    {
        private static readonly IDictionary<string, string> EmptyClaims = new Dictionary<string, string>();
        private const string ContentTypeApplicationJson = "application/json";
        private const string ContentTypeTextPlain = "text/plain";
        private const string ContentTypeHeader = "Content-Type";
        private readonly NtradaConfiguration _configuration;
        private readonly IPayloadBuilder _payloadBuilder;
        private readonly IPayloadManager _payloadManager;
        private readonly IDownstreamBuilder _downstreamBuilder;

        public RequestProcessor(NtradaConfiguration configuration, IPayloadBuilder payloadBuilder,
            IPayloadManager payloadManager, IDownstreamBuilder downstreamBuilder)
        {
            _configuration = configuration;
            _payloadBuilder = payloadBuilder;
            _payloadManager = payloadManager;
            _downstreamBuilder = downstreamBuilder;
        }

        public async Task<ExecutionData> ProcessAsync(RouteConfig routeConfig,
            HttpRequest request, HttpResponse response, RouteData data)
        {
            request.Headers.TryGetValue(ContentTypeHeader, out var contentType);
            var contentTypeValue = contentType.ToString().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(contentTypeValue) || contentTypeValue.Contains(ContentTypeTextPlain))
            {
                contentTypeValue = ContentTypeApplicationJson;
            }

            var (requestId, resourceId, traceId) = GenerateIds(request, routeConfig);
            var route = routeConfig.Route;
            var skipPayload = route.Use == "downstream" && (string.IsNullOrWhiteSpace(route.DownstreamMethod) ||
                                                            route.DownstreamMethod == "get" ||
                                                            route.DownstreamMethod == "delete");
            var payload = skipPayload
                ? null
                : await _payloadBuilder.BuildAsync(resourceId, routeConfig.Route, request, data);

            var executionData = new ExecutionData
            {
                RequestId = requestId,
                ResourceId = resourceId,
                TraceId = traceId,
                UserId = request.HttpContext.User?.Identity?.Name,
                Claims = request.HttpContext.User?.Claims?.ToDictionary(c => c.Type, c => c.Value) ?? EmptyClaims,
                ContentType = contentTypeValue,
                Route = routeConfig.Route,
                Request = request,
                Response = response,
                Data = data,
                Downstream = _downstreamBuilder.GetDownstream(routeConfig, request, data),
                Payload = payload?.Payload
            };

            if (skipPayload || payload is null)
            {
                return executionData;
            }

            executionData.ValidationErrors = await _payloadManager.GetValidationErrorsAsync(payload);

            return executionData;
        }

        private (string, string, string) GenerateIds(HttpRequest request, RouteConfig routeConfig)
        {
            var requestId = string.Empty;
            var resourceId = string.Empty;
            var traceId = string.Empty;
            if (routeConfig.Route.GenerateRequestId == true ||
                _configuration.GenerateRequestId == true && (routeConfig.Route.GenerateRequestId != false))
            {
                requestId = Guid.NewGuid().ToString("N");
            }

            if (routeConfig.Route.ResourceId?.Generate == true ||
                _configuration.ResourceId?.Generate == true && (routeConfig.Route.ResourceId?.Generate != false))
            {
                resourceId = Guid.NewGuid().ToString("N");
            }

            if (routeConfig.Route.GenerateTraceId == true ||
                _configuration.GenerateTraceId == true && (routeConfig.Route.GenerateTraceId != false))
            {
                traceId = request.HttpContext.TraceIdentifier;
            }

            return (requestId, resourceId, traceId);
        }
    }
}