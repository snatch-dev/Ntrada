using System;
using Ntrada.Core;
using Ntrada.Core.Configuration;
using Ntrada.Options;

namespace Ntrada.Routing
{
    internal sealed class RouteConfigurator : IRouteConfigurator
    {
        private static readonly string LoadBalancerPattern = "load_balancer";
        private readonly NtradaOptions _options;

        public RouteConfigurator(NtradaOptions options)
        {
            _options = options;
        }

        public RouteConfig Configure(Module module, Route route)
            => new RouteConfig
            {
                Route = route,
                Downstream = GetDownstream(module, route)
            };

        private string GetDownstream(Module module, Route route)
        {
            if (string.IsNullOrWhiteSpace(route.Downstream))
            {
                return null;
            }

            var loadBalancerEnabled = _options.LoadBalancer?.Enabled == true;
            var loadBalancerUrl = _options.LoadBalancer?.Url;
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
                return SetProtocol(route.Downstream);
            }

            if (service is null)
            {
                throw new ArgumentException($"Service for: '{basePath}' was not defined.", nameof(service.Url));
            }

            if (_options.UseLocalUrl)
            {
                if (string.IsNullOrWhiteSpace(service.LocalUrl))
                {
                    throw new ArgumentException($"Local URL for: '{basePath}' cannot be empty if use_local_url = true.",
                        nameof(service.LocalUrl));
                }

                return SetProtocol(route.Downstream.Replace(basePath, service.LocalUrl));
            }

            if (!string.IsNullOrWhiteSpace(service.LocalUrl) && string.IsNullOrWhiteSpace(service.Url))
            {
                return SetProtocol(route.Downstream.Replace(basePath, service.LocalUrl));
            }

            if (string.IsNullOrWhiteSpace(service.Url))
            {
                throw new ArgumentException($"Service URL for: '{basePath}' cannot be empty.", nameof(service.Url));
            }

            if (!loadBalancerEnabled)
            {
                return SetProtocol(route.Downstream.Replace(basePath, service.Url));
            }

            if (!service.Url.StartsWith(LoadBalancerPattern, StringComparison.InvariantCultureIgnoreCase))
            {
                return SetProtocol(route.Downstream.Replace(basePath, service.Url));
            }

            var serviceUrl = service.Url.Replace(LoadBalancerPattern, _options.LoadBalancer.Url);

            return SetProtocol(route.Downstream.Replace(basePath, serviceUrl));
        }

        private static string SetProtocol(string service) => service.StartsWith("http") ? service : $"http://{service}";
    }
}