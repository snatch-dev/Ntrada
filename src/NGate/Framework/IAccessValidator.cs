using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NGate.Framework
{
    public interface IAccessValidator
    {
        Task<bool> IsAuthenticatedAsync(HttpRequest request, RouteConfig routeConfig);
        bool IsAuthorized(ClaimsPrincipal user, RouteConfig routeConfig);
    }
}