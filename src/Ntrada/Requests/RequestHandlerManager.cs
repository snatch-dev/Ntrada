using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ntrada.Requests
{
    public class RequestHandlerManager : IRequestHandlerManager
    {
        private readonly ConcurrentDictionary<string, IHandler>
            _handlers = new ConcurrentDictionary<string, IHandler>();

        public void AddHandler(string name, IHandler handler)
        {
            _handlers.TryAdd(name, handler);
        }

        public async Task HandleAsync(string handler, HttpRequest request, HttpResponse response, RouteData routeData,
            RouteConfig routeConfig)
        {
            if (!_handlers.TryGetValue(handler, out var instance))
            {
                throw new Exception($"Handler: '{handler}' was not found.");
            }

            await instance.HandleAsync(request, response, routeData, routeConfig);
        }
    }
}