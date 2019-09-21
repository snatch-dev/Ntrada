using System.Security.Claims;

namespace Ntrada
{
    public interface IAuthorizationManager
    {
        bool IsAuthorized(ClaimsPrincipal user, RouteConfig routeConfig);
    }
}