using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Ntrada.Handlers
{
    public class DispatcherHandler : IHandler
    {
        private readonly IRequestProcessor _requestProcessor;
        private readonly IPayloadValidator _payloadValidator;
        private readonly IDictionary<string, IExtension> _extensions;
        private readonly ILogger<DispatcherHandler> _logger;

        public DispatcherHandler(IRequestProcessor requestProcessor, IPayloadValidator payloadValidator,
            IExtensionManager extensionManager, ILogger<DispatcherHandler> logger)
        {
            _requestProcessor = requestProcessor;
            _payloadValidator = payloadValidator;
            _extensions = extensionManager.Extensions;
            _logger = logger;
        }

        public async Task HandleAsync(HttpRequest request, HttpResponse response, RouteData routeData,
            RouteConfig routeConfig)
        {
            const string name = "dispatcher";
            if (!_extensions.ContainsKey(name))
            {
                throw new InvalidOperationException($"Extension for: '{name}' was not found.");
            }

            var executionData = await _requestProcessor.ProcessAsync(routeConfig, request, response, routeData);
            if (!await _payloadValidator.TryValidate(executionData, response))
            {
                return;
            }

            var dispatcher = _extensions[name];
            var traceId = request.HttpContext.TraceIdentifier;
            _logger.LogInformation($"Dispatching a message: {routeConfig.Route.RoutingKey} to the exchange: " +
                                   $"{routeConfig.Route.Exchange} [Trace ID: {traceId}]");
            await dispatcher.ExecuteAsync(executionData);
            response.Headers.Add("Request-ID", executionData.RequestId);
            if (executionData.Route.Method == "post")
            {
                response.Headers.Add("Resource-ID", executionData.ResourceId);
            }

            response.Headers.Add("Trace-ID", executionData.Request.HttpContext.TraceIdentifier);
            response.StatusCode = 202;
        }
    }
}