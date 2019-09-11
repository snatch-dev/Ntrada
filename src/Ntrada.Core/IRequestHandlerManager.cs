using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ntrada.Core
{
    public interface IRequestHandlerManager
    {
        IHandler Get(string name);
        void AddHandler(string name, IHandler handler);

        Task HandleAsync(string handler, HttpRequest request, HttpResponse response, RouteData routeData,
            RouteConfig routeConfig);
    }
}