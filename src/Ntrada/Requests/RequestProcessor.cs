using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ntrada.Options;

namespace Ntrada.Requests
{
    internal sealed class RequestProcessor : IRequestProcessor
    {
        private static readonly string[] SkipPayloadMethods = {"get", "delete", "head", "options", "trace"};
        private static readonly IDictionary<string, string> EmptyClaims = new Dictionary<string, string>();
        private const string ContentTypeApplicationJson = "application/json";
        private const string ContentTypeTextPlain = "text/plain";
        private const string ContentTypeHeader = "Content-Type";
        private readonly NtradaOptions _options;
        private readonly IPayloadTransformer _payloadTransformer;
        private readonly IPayloadBuilder _payloadBuilder;
        private readonly IPayloadValidator _payloadValidator;
        private readonly IDownstreamBuilder _downstreamBuilder;

        public RequestProcessor(NtradaOptions options, IPayloadTransformer payloadTransformer,
            IPayloadBuilder payloadBuilder, IPayloadValidator payloadValidator, IDownstreamBuilder downstreamBuilder)
        {
            _options = options;
            _payloadTransformer = payloadTransformer;
            _payloadBuilder = payloadBuilder;
            _payloadValidator = payloadValidator;
            _downstreamBuilder = downstreamBuilder;
        }

        public async Task<ExecutionData> ProcessAsync(RouteConfig routeConfig, HttpContext context)
        {
            context.Request.Headers.TryGetValue(ContentTypeHeader, out var contentType);
            var contentTypeValue = contentType.ToString().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(contentTypeValue) || contentTypeValue.Contains(ContentTypeTextPlain))
            {
                contentTypeValue = ContentTypeApplicationJson;
            }

            var (requestId, resourceId, traceId) = GenerateIds(context.Request, routeConfig);
            var route = routeConfig.Route;
            var skipPayload = route.Use == "downstream" && SkipPayloadMethods.Contains(route.DownstreamMethod);
            var routeData = context.GetRouteData();
            var hasTransformations = !skipPayload && _payloadTransformer.HasTransformations(resourceId, route);
            var payload = hasTransformations
                ? _payloadTransformer.Transform(await _payloadBuilder.BuildRawAsync(context.Request),
                    resourceId, route, context.Request, routeData)
                : null;

            var executionData = new ExecutionData
            {
                RequestId = requestId,
                ResourceId = resourceId,
                TraceId = traceId,
                UserId = context.Request.HttpContext.User?.Identity?.Name,
                Claims = context.Request.HttpContext.User?.Claims?.ToDictionary(c => c.Type, c => c.Value) ??
                         EmptyClaims,
                ContentType = contentTypeValue,
                Route = routeConfig.Route,
                Context = context,
                Data = routeData,
                Downstream = _downstreamBuilder.GetDownstream(routeConfig, context.Request, routeData),
                Payload = payload?.Payload,
                HasPayload = hasTransformations
            };

            if (skipPayload || payload is null)
            {
                return executionData;
            }

            executionData.ValidationErrors = await _payloadValidator.GetValidationErrorsAsync(payload);

            return executionData;
        }

        private (string, string, string) GenerateIds(HttpRequest request, RouteConfig routeConfig)
        {
            var requestId = string.Empty;
            var resourceId = string.Empty;
            var traceId = string.Empty;
            if (routeConfig.Route.GenerateRequestId == true ||
                _options.GenerateRequestId == true && routeConfig.Route.GenerateRequestId != false)
            {
                requestId = Guid.NewGuid().ToString("N");
            }

            if (!(request.Method is "GET" || request.Method is "DELETE") &&
                (routeConfig.Route.ResourceId?.Generate == true ||
                 _options.ResourceId?.Generate == true && routeConfig.Route.ResourceId?.Generate != false))
            {
                resourceId = Guid.NewGuid().ToString("N");
            }

            if (routeConfig.Route.GenerateTraceId == true ||
                _options.GenerateTraceId == true && routeConfig.Route.GenerateTraceId != false)
            {
                traceId = request.HttpContext.TraceIdentifier;
            }

            return (requestId, resourceId, traceId);
        }
    }
}