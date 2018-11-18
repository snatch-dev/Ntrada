using System.Collections.Generic;
using System.Linq;

namespace NGate.Framework
{
    public class RouteConfigurator : IRouteConfigurator
    {
        private readonly Configuration _configuration;

        public RouteConfigurator(Configuration configuration)
        {
            _configuration = configuration;
        }

        public RouteConfig Configure(Module module, Route route)
            => new RouteConfig
            {
                Route = route,
                Claims = GetClaims(module, route),
                Downstream = GetDownstream(module, route)
            };

        private IDictionary<string, string> GetClaims(Module module, Route route)
        {
            if (route.Claims == null || !route.Claims.Any())
            {
                return new Dictionary<string, string>();
            }

            var claims = _configuration.Config?.Authentication?.Claims;
            if (claims == null || !claims.Any())
            {
                return route.Claims;
            }

            var mappedClaims = new Dictionary<string, string>();
            foreach (var claim in route.Claims)
            {
                var key = claim.Key;
                if (claims.TryGetValue(claim.Key, out var value))
                {
                    key = value;
                }

                mappedClaims[key] = claim.Value;
            }

            return mappedClaims;
        }

        private string GetDownstream(Module module, Route route)
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