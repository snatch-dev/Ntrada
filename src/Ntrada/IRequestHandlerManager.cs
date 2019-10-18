using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Ntrada
{
    public interface IRequestHandlerManager
    {
        IHandler Get(string name);
        void AddHandler(string name, IHandler handler);
        Task HandleAsync(string handler, HttpContext context, RouteConfig routeConfig);
    }
}