using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ntrada.Core;
using Route = Ntrada.Core.Configuration.Route;

namespace Ntrada.Handlers
{
    public class ReturnValueHandler : IHandler
    {
        public string GetInfo(Route route) => $"return a value: '{route.ReturnValue}'";

        public async Task HandleAsync(HttpRequest request, HttpResponse response, RouteData routeData,
            RouteConfig routeConfig)
            => await response.WriteAsync(routeConfig.Route.ReturnValue);
    }
}