using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ntrada
{
    public interface IHandler
    {
        Task HandleAsync(HttpRequest request, HttpResponse response, RouteData routeData, RouteConfig routeConfig);
    }
}