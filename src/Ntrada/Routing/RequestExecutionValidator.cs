using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ntrada.Routing
{
    internal sealed class RequestExecutionValidator : IRequestExecutionValidator
    {
        private readonly IAuthenticationManager _authenticationManager;
        private readonly IAuthorizationManager _authorizationManager;
        private readonly ILogger<RequestExecutionValidator> _logger;

        public RequestExecutionValidator(IAuthenticationManager authenticationManager,
            IAuthorizationManager authorizationManager, ILogger<RequestExecutionValidator> logger)
        {
            _authenticationManager = authenticationManager;
            _authorizationManager = authorizationManager;
            _logger = logger;
        }

        public async Task<bool> TryExecuteAsync(HttpContext context, RouteConfig routeConfig)
        {
            var traceId = context.TraceIdentifier;
            var isAuthenticated = await _authenticationManager.TryAuthenticateAsync(context.Request, routeConfig);
            if (!isAuthenticated)
            {
                _logger.LogWarning($"Unauthorized request to: {routeConfig.Route.Upstream} [Trace ID: {traceId}]");
                context.Response.StatusCode = 401;

                return false;
            }

            if (_authorizationManager.IsAuthorized(context.User, routeConfig))
            {
                return true;
            }

            _logger.LogWarning($"Forbidden request to: {routeConfig.Route.Upstream} by user: " +
                               $"{context.User.Identity.Name} [Trace ID: {traceId}]");
            context.Response.StatusCode = 403;

            return false;
        }
    }
}