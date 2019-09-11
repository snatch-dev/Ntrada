using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Ntrada.Core;

namespace Ntrada.Routing
{
    public class RequestExecutionValidator : IRequestExecutionValidator
    {
        private readonly IAccessValidator _accessValidator;
        private readonly ILogger<RequestExecutionValidator> _logger;

        public RequestExecutionValidator(IAccessValidator accessValidator, ILogger<RequestExecutionValidator> logger)
        {
            _accessValidator = accessValidator;
            _logger = logger;
        }

        public async Task<bool> TryExecuteAsync(HttpRequest request, HttpResponse response, RouteData data,
            RouteConfig routeConfig)
        {
            var traceId = request.HttpContext.TraceIdentifier;
            var isAuthenticated = await _accessValidator.IsAuthenticatedAsync(request, routeConfig);
            if (!isAuthenticated)
            {
                _logger.LogWarning($"Unauthorized request to: {routeConfig.Route.Upstream} [Trace ID: {traceId}]");
                response.StatusCode = 401;

                return false;
            }

            if (_accessValidator.IsAuthorized(request.HttpContext.User, routeConfig))
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