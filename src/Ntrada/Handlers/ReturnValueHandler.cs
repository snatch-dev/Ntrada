using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Route = Ntrada.Configuration.Route;

namespace Ntrada.Handlers
{
    internal sealed class ReturnValueHandler : IHandler
    {
        public string GetInfo(Route route) => $"return a value: '{route.ReturnValue}'";

        public Task HandleAsync(HttpContext context, RouteConfig config)
            => context.Response.WriteAsync(config.Route?.ReturnValue ?? string.Empty);
    }
}