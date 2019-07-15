using System;
using System.Collections.Generic;
using System.Linq;
using Ntrada.Configuration;

namespace Ntrada.Routing
{
    public class RouteConfigurator : IRouteConfigurator
    {
        private static readonly string LoadBalancerPattern = "load_balancer";
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

        private string GetDownstream(Module module, Route route)
        {
            if (string.IsNullOrWhiteSpace(route.Downstream))
            {
                return null;
            }

            var loadBalancerEnabled = _configuration.LoadBalancer?.Enabled == true;
            var loadBalancerUrl = _configuration.LoadBalancer?.Url;
            if (loadBalancerEnabled)
            {
                if (string.IsNullOrWhiteSpace(loadBalancerUrl))
                {
                    throw new ArgumentException("Load balancer URL cannot be empty.", nameof(loadBalancerUrl));
                }
            }

            var basePath = route.Downstream.Contains("/")
                ? route.Downstream.Split('/')[0]
                : route.Downstream;

            var hasService = module.Services.TryGetValue(basePath, out var service);
            if (!hasService)
            {
                return UpdateProtocol(route.Downstream);
            }

            if (service is null)
            {
                throw new ArgumentException($"Service for: '{basePath}' was not defined.", nameof(service.Url));
            }

            if (_configuration.UseLocalUrl)
            {
                if (string.IsNullOrWhiteSpace(service.LocalUrl))
                {
                    throw new ArgumentException($"Local URL for: '{basePath}' cannot be empty if use_local_url = true.", nameof(service.LocalUrl));
                }
                
                return UpdateProtocol(route.Downstream.Replace(basePath, service.LocalUrl));
            }

            if (!string.IsNullOrWhiteSpace(service.LocalUrl) && string.IsNullOrWhiteSpace(service.Url))
            {
                return UpdateProtocol(route.Downstream.Replace(basePath, service.LocalUrl));
            }

            if (string.IsNullOrWhiteSpace(service.Url))
            {
                throw new ArgumentException($"Service URL for: '{basePath}' cannot be empty.", nameof(service.Url));
            }

            if (!loadBalancerEnabled)
            {
                return UpdateProtocol(route.Downstream.Replace(basePath, service.Url));
            }

            if (!service.Url.StartsWith(LoadBalancerPattern, StringComparison.InvariantCultureIgnoreCase))
            {
                return UpdateProtocol(route.Downstream.Replace(basePath, service.Url));
            }

            var serviceUrl = service.Url.Replace(LoadBalancerPattern, _configuration.LoadBalancer.Url);

            return UpdateProtocol(route.Downstream.Replace(basePath, serviceUrl));
        }

        private static string UpdateProtocol(string service) =>
            service.StartsWith("http") ? service : $"http://{service}";
    }
}