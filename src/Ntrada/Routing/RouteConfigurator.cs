using System.Collections.Generic;
using System.Linq;
using Ntrada.Configuration;

namespace Ntrada.Routing
{
    public class RouteConfigurator : IRouteConfigurator
    {
        private readonly IDictionary<string, string> _claims;
        private readonly NtradaConfiguration _configuration;

        public RouteConfigurator(NtradaConfiguration configuration)
        {
            _configuration = configuration;
            _claims = _configuration?.Auth?.Claims ?? new Dictionary<string, string>();
        }

        public RouteConfig Configure(Module module, Route route)
            => new RouteConfig
            {
                Route = route,
                Downstream = GetDownstream(module, route),
                Claims = GetClaims(route)
            };

        private IDictionary<string, string> GetClaims(Route route)
        {
            if (route.Claims is null || !route.Claims.Any() || !_claims.Any())
            {
                return new Dictionary<string, string>();
            }

            return route.Claims.ToDictionary(c => GetClaimKey(c.Key), c => c.Value);
        }

        private string GetClaimKey(string claim)
            => _claims.TryGetValue(claim, out var value) ? value : claim;

        private static string GetDownstream(Module module, Route route)
        {
            if (string.IsNullOrWhiteSpace(route.Downstream))
            {
                return null;
            }

            var basePath = route.Downstream.Contains("/")
                ? route.Downstream.Split('/')[0]
                : route.Downstream;

            var servicePath = module.Services.TryGetValue(basePath, out var service)
                ? route.Downstream.Replace(basePath, service.Url)
                : route.Downstream;

            return servicePath.StartsWith("http") ? servicePath : $"http://{servicePath}";
        }
    }
}