using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Route = Ntrada.Configuration.Route;

namespace Ntrada
{
    public interface IHandler
    {
        string GetInfo(Route route);
        Task HandleAsync(HttpRequest request, HttpResponse response, RouteData data, RouteConfig config);
    }
}