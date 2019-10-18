using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Ntrada
{
    public interface IRequestProcessor
    {
        Task<ExecutionData> ProcessAsync(RouteConfig routeConfig, HttpContext context);
    }
}