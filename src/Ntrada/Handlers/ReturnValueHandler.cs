using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ntrada.Handlers
{
    public class ReturnValueHandler : IHandler
    {
        public async Task HandleAsync(HttpRequest request, HttpResponse response, RouteData routeData,
            RouteConfig routeConfig)
            => await response.WriteAsync(routeConfig.Route.ReturnValue);
    }
}