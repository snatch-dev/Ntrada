using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<RabbitMqHandler> _logger;

        private const string RequestIdHeader = "Request-ID";
        private const string ResourceIdHeader = "Resource-ID";
        private const string TraceIdHeader = "Trace-ID";
        private const string ConfigRoutingKey = "routing_key";
        private const string ConfigExchange = "exchange";

        public RabbitMqHandler(IRabbitMqClient rabbitMqClient, IRequestProcessor requestProcessor,
            IPayloadBuilder payloadBuilder, IPayloadValidator payloadValidator, IContextBuilder contextBuilder,
            ILogger<RabbitMqHandler> logger)
        {
            _rabbitMqClient = rabbitMqClient;
            _requestProcessor = requestProcessor;
            _payloadBuilder = payloadBuilder;
            _payloadValidator = payloadValidator;
            _contextBuilder = contextBuilder;
            _logger = logger;
        }

        public string GetInfo(Route route) => $"send a message to the exchange: '{route.Config["routing_key"]}'";

        public async Task HandleAsync(HttpRequest request, HttpResponse response, RouteData data, RouteConfig config)
        {
            var executionData = await _requestProcessor.ProcessAsync(config, request, response, data);
            if (!await _payloadValidator.TryValidate(executionData, response))
            {
                return;
            }

            var traceId = request.HttpContext.TraceIdentifier;
            var routeConfig = executionData.Route.Config;
            var routingKey = routeConfig[ConfigRoutingKey];
            var exchange = routeConfig[ConfigExchange];
            var message = executionData.HasPayload
                ? executionData.Payload
                : await _payloadBuilder.BuildJsonAsync<object>(request);
            var context = _contextBuilder.Build(executionData);
            var hasTraceId = !string.IsNullOrWhiteSpace(traceId);

            _logger.LogInformation($"Sending a message: {routingKey} to the exchange: {exchange}" +
                                   (hasTraceId ? $" [Trace ID: {traceId}]" : string.Empty));

            _rabbitMqClient.Send(message, routingKey, exchange, context);

            if (!string.IsNullOrWhiteSpace(executionData.RequestId))
            {
                response.Headers.Add(RequestIdHeader, executionData.RequestId);
            }

            if (!string.IsNullOrWhiteSpace(executionData.ResourceId) && executionData.Route.Method is "post")
            {
                response.Headers.Add(ResourceIdHeader, executionData.ResourceId);
            }

            if (hasTraceId)
            {
                response.Headers.Add(TraceIdHeader, traceId);
            }

            response.StatusCode = 202;
        }
    }
}