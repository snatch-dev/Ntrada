using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Ntrada.Core;
using Route = Ntrada.Core.Configuration.Route;

namespace Ntrada.Extensions.RabbitMq
{
    public class RabbitMqHandler : IHandler
    {
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly IRequestProcessor _requestProcessor;
        private readonly IPayloadValidator _payloadValidator;
        private readonly IContextBuilder _contextBuilder;
        private readonly ILogger<RabbitMqHandler> _logger;

        public RabbitMqHandler(IRabbitMqClient rabbitMqClient, IRequestProcessor requestProcessor,
            IPayloadValidator payloadValidator, IContextBuilder contextBuilder, ILogger<RabbitMqHandler> logger)
        {
            _rabbitMqClient = rabbitMqClient;
            _requestProcessor = requestProcessor;
            _payloadValidator = payloadValidator;
            _contextBuilder = contextBuilder;
            _logger = logger;
        }

        public string GetInfo(Route route) => $"send a message to the exchange: '{route.Config["routing_key"]}'";

        public async Task HandleAsync(HttpRequest request, HttpResponse response, RouteData routeData,
            RouteConfig routeConfig)
        {
            var executionData = await _requestProcessor.ProcessAsync(routeConfig, request, response, routeData);
            if (!await _payloadValidator.TryValidate(executionData, response))
            {
                return;
            }

            var traceId = request.HttpContext.TraceIdentifier;
            var config = executionData.Route.Config;
            var routingKey = config["routing_key"];
            var exchange = config["exchange"];
            var message = executionData.Payload;
            var context = _contextBuilder.Build(executionData);
            var hasTraceId = !string.IsNullOrWhiteSpace(traceId);
            
            _logger.LogInformation($"Sending a message: {routingKey} to the exchange: {exchange}" +
                                   (hasTraceId ? $" [Trace ID: {traceId}]" : string.Empty));
            
            _rabbitMqClient.Send(message, routingKey, exchange, context);
            
            if (!string.IsNullOrWhiteSpace(executionData.RequestId))
            {
                response.Headers.Add("Request-ID", executionData.RequestId);
            }

            if (!string.IsNullOrWhiteSpace(executionData.ResourceId) && executionData.Route.Method == "post")
            {
                response.Headers.Add("Resource-ID", executionData.ResourceId);
            }

            if (hasTraceId)
            {
                response.Headers.Add("Trace-ID", traceId);
            }

            response.StatusCode = 202;
        }
    }
}