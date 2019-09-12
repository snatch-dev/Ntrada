using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ntrada.Core;

namespace Ntrada
{
    internal interface IRequestExecutionValidator
    {
        Task<bool> TryExecuteAsync(HttpRequest request, HttpResponse response, RouteData data, RouteConfig routeConfig);
    }
}