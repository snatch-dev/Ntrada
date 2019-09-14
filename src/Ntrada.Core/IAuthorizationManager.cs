using System.Security.Claims;

namespace Ntrada.Core
{
    public interface IAuthorizationManager
    {
        bool IsAuthorized(ClaimsPrincipal user, RouteConfig routeConfig);
    }
}