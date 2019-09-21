using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Ntrada
{
    public interface IAuthenticationManager
    {
        Task<bool> TryAuthenticateAsync(HttpRequest request, RouteConfig routeConfig);
    }
}