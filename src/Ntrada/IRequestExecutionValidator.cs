using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Ntrada
{
    internal interface IRequestExecutionValidator
    {
        Task<bool> TryExecuteAsync(HttpContext context, RouteConfig routeConfig);
    }
}