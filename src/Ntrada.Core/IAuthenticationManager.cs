using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Ntrada.Core
{
    public interface IAuthenticationManager
    {
        Task<bool> IsAuthenticatedAsync(HttpRequest request, RouteConfig routeConfig);
    }
}