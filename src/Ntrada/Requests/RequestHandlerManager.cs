using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Ntrada.Core;

namespace Ntrada.Requests
{
    internal sealed class RequestHandlerManager : IRequestHandlerManager
    {
        private readonly ILogger<IRequestHandlerManager> _logger;

        private static readonly ConcurrentDictionary<string, IHandler> Handlers =
            new ConcurrentDictionary<string, IHandler>();

        public RequestHandlerManager(ILogger<RequestHandlerManager> logger)
        {
            _logger = logger;
        }

        public IHandler Get(string name) => Handlers.TryGetValue(name, out var handler) ? handler : null;

        public void AddHandler(string name, IHandler handler)
        {
            if (Handlers.TryAdd(name, handler))
            {
                _logger.LogInformation($"Added a request handler: '{name}'");
                return;
            }

            _logger.LogError($"Couldn't add a request handler: '{name}'");
        }

        public async Task HandleAsync(string handler, HttpRequest request, HttpResponse response, RouteData routeData,
            RouteConfig routeConfig)
        {
            if (!Handlers.TryGetValue(handler, out var instance))
            {
                throw new Exception($"Handler: '{handler}' was not found.");
            }

            await instance.HandleAsync(request, response, routeData, routeConfig);
        }
    }
}