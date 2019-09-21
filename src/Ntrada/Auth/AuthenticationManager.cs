using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
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


        public async Task<bool> TryAuthenticateAsync(HttpRequest request, RouteConfig routeConfig)
        {
            if (_options.Auth is null || !_options.Auth.Enabled || _options.Auth?.Global != true &&
                routeConfig.Route?.Auth != true)
            {
                return true;
            }

            var result = await request.HttpContext.AuthenticateAsync();

            return result.Succeeded;
        }
    }
}