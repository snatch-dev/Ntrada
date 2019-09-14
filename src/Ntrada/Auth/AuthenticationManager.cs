using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Ntrada.Core;
using Ntrada.Options;

namespace Ntrada.Auth
{
    internal sealed class AuthenticationManager : IAuthenticationManager
    {
        private readonly NtradaOptions _options;

        public AuthenticationManager(NtradaOptions options)
        {
            _options = options;
        }


        public async Task<bool> IsAuthenticatedAsync(HttpRequest request, RouteConfig routeConfig)
        {
            if (_options.Auth?.Global != true
                || routeConfig.Route.Auth.HasValue && routeConfig.Route.Auth == false)
            {
                return true;
            }

            var result = await request.HttpContext.AuthenticateAsync();

            return result.Succeeded;
        }
    }
}