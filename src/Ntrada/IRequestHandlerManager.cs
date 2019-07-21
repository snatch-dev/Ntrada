using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ntrada
{
    public interface IRequestHandlerManager
    {
        void AddHandler(string name, IHandler handler);

        Task HandleAsync(string handler, HttpRequest request, HttpResponse response, RouteData routeData,
            RouteConfig routeConfig);
    }
}