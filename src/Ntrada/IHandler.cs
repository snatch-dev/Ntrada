using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Route = Ntrada.Configuration.Route;

namespace Ntrada
{
    public interface IHandler
    {
        string GetInfo(Route route);
        Task HandleAsync(HttpContext context, RouteConfig config);
    }
}