using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Ntrada.Core;

namespace Ntrada
{
    public interface IAccessValidator
    {
        Task<bool> IsAuthenticatedAsync(HttpRequest request, RouteConfig routeConfig);
        bool IsAuthorized(ClaimsPrincipal user, RouteConfig routeConfig);
    }
}