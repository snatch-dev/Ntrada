using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Route = Ntrada.Configuration.Route;

namespace Ntrada.Handlers
{
    internal sealed class ReturnValueHandler : IHandler
    {
        public string GetInfo(Route route) => $"return a value: '{route.ReturnValue}'";

        public async Task HandleAsync(HttpRequest request, HttpResponse response, RouteData data, RouteConfig config)
            => await response.WriteAsync(config.Route?.ReturnValue ?? string.Empty);
    }
}