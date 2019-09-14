using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Ntrada.Core;

namespace Ntrada.Routing
{
    public class RequestExecutionValidator : IRequestExecutionValidator
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

        public async Task<bool> TryExecuteAsync(HttpRequest request, HttpResponse response, RouteData data,
            RouteConfig routeConfig)
        {
            var traceId = request.HttpContext.TraceIdentifier;
            var isAuthenticated = await _authenticationManager.IsAuthenticatedAsync(request, routeConfig);
            if (!isAuthenticated)
            {
                _logger.LogWarning($"Unauthorized request to: {routeConfig.Route.Upstream} [Trace ID: {traceId}]");
                response.StatusCode = 401;

                return false;
            }

            if (_authorizationManager.IsAuthorized(request.HttpContext.User, routeConfig))
            {
                return true;
            }

            _logger.LogWarning($"Forbidden request to: {routeConfig.Route.Upstream} by user: " +
                               $"{request.HttpContext.User.Identity.Name} [Trace ID: {traceId}]");
            response.StatusCode = 403;

            return false;
        }
    }
}