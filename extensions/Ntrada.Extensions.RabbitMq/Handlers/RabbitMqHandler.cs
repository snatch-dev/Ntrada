using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Route = Ntrada.Configuration.Route;

namespace Ntrada.Extensions.RabbitMq.Handlers
{
    public sealed class RabbitMqHandler : IHandler
    {
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly IRequestProcessor _requestProcessor;
        private readonly IPayloadBuilder _payloadBuilder;
        private readonly IPayloadValidator _payloadValidator;
        private readonly IContextBuilder _contextBuilder;
        private readonly ISpanContextBuilder _spanContextBuilder;
        private const string RequestIdHeader = "Request-ID";
        private const string ResourceIdHeader = "Resource-ID";
        private const string TraceIdHeader = "Trace-ID";
        private const string ConfigRoutingKey = "routing_key";
        private const string ConfigExchange = "exchange";

        public RabbitMqHandler(IRabbitMqClient rabbitMqClient, IContextBuilder contextBuilder,
            ISpanContextBuilder spanContextBuilder, IRequestProcessor requestProcessor, IPayloadBuilder payloadBuilder,
            IPayloadValidator payloadValidator)
        {
            _rabbitMqClient = rabbitMqClient;
            _contextBuilder = contextBuilder;
            _spanContextBuilder = spanContextBuilder;
            _requestProcessor = requestProcessor;
            _payloadBuilder = payloadBuilder;
            _payloadValidator = payloadValidator;
        }

        public string GetInfo(Route route) => $"send a message to the exchange: '{route.Config["routing_key"]}'";

        public async Task HandleAsync(HttpContext context, RouteConfig config)
        {
            var executionData = await _requestProcessor.ProcessAsync(config, context);
            if (!executionData.IsPayloadValid)
            {
                await _payloadValidator.TryValidate(executionData, context.Response);
                return;
            }

            var traceId = context.TraceIdentifier;
            var routeConfig = executionData.Route.Config;
            var routingKey = routeConfig[ConfigRoutingKey];
            var exchange = routeConfig[ConfigExchange];
            var message = executionData.HasPayload
                ? executionData.Payload
                : await _payloadBuilder.BuildJsonAsync<object>(context.Request);
            var messageContext = _contextBuilder.Build(executionData);
            var hasTraceId = !string.IsNullOrWhiteSpace(traceId);
            var spanContext = _spanContextBuilder.Build(executionData);
            var correlationId = executionData.RequestId;

            _rabbitMqClient.Send(message, routingKey, exchange, correlationId: correlationId, spanContext: spanContext,
                messageContext: messageContext);

            if (!string.IsNullOrWhiteSpace(executionData.RequestId))
            {
                context.Response.Headers.Add(RequestIdHeader, executionData.RequestId);
            }

            if (!string.IsNullOrWhiteSpace(executionData.ResourceId) && context.Request.Method is "POST")
            {
                context.Response.Headers.Add(ResourceIdHeader, executionData.ResourceId);
            }

            if (hasTraceId)
            {
                context.Response.Headers.Add(TraceIdHeader, traceId);
            }

            context.Response.StatusCode = 202;
        }
    }
}