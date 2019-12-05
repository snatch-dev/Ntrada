using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Ntrada.Hooks;
using Route = Ntrada.Configuration.Route;

namespace Ntrada.Extensions.RabbitMq.Handlers
{
    public sealed class RabbitMqHandler : IHandler
    {
        private const string RequestIdHeader = "Request-ID";
        private const string ResourceIdHeader = "Resource-ID";
        private const string TraceIdHeader = "Trace-ID";
        private const string ConfigRoutingKey = "routing_key";
        private const string ConfigExchange = "exchange";
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly IRequestProcessor _requestProcessor;
        private readonly IPayloadBuilder _payloadBuilder;
        private readonly IPayloadValidator _payloadValidator;
        private readonly IContextBuilder _contextBuilder;
        private readonly ISpanContextBuilder _spanContextBuilder;
        private readonly IEnumerable<IRequestHook> _requestHooks;
        private readonly IEnumerable<IResponseHook> _responseHooks;

        public RabbitMqHandler(IRabbitMqClient rabbitMqClient, IContextBuilder contextBuilder,
            ISpanContextBuilder spanContextBuilder, IRequestProcessor requestProcessor, IPayloadBuilder payloadBuilder,
            IPayloadValidator payloadValidator, IServiceProvider serviceProvider)
        {
            _rabbitMqClient = rabbitMqClient;
            _contextBuilder = contextBuilder;
            _spanContextBuilder = spanContextBuilder;
            _requestProcessor = requestProcessor;
            _payloadBuilder = payloadBuilder;
            _payloadValidator = payloadValidator;
            _requestHooks = serviceProvider.GetServices<IRequestHook>();
            _responseHooks = serviceProvider.GetServices<IResponseHook>();
        }

        public string GetInfo(Route route) => $"send a message to the exchange: '{route.Config["routing_key"]}'";

        public async Task HandleAsync(HttpContext context, RouteConfig config)
        {
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

            if (_responseHooks is {})
            {
                foreach (var hook in _responseHooks)
                {
                    if (hook is null)
                    {
                        continue;
                    }

                    await hook.InvokeAsync(context.Response, executionData);
                }
            }

            context.Response.StatusCode = 202;
        }
    }
}