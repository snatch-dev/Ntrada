using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ntrada
{
    public interface IRequestProcessor
    {
        Task<ExecutionData> ProcessAsync(RouteConfig routeConfig,
            HttpRequest request, HttpResponse response, RouteData data);
    }
}