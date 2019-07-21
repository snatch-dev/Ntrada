using Microsoft.Extensions.Logging;
using Ntrada.Configuration;

namespace Ntrada.Routing
{
    public class UpstreamBuilder : IUpstreamBuilder
    {
        private readonly IRouteConfigurator _routeConfigurator;
        private readonly NtradaConfiguration _configuration;
        private readonly ILogger<UpstreamBuilder> _logger;

        public UpstreamBuilder(IRouteConfigurator routeConfigurator, NtradaConfiguration configuration,
            ILogger<UpstreamBuilder> logger)
        {
            _routeConfigurator = routeConfigurator;
            _configuration = configuration;
            _logger = logger;
        }

        public string Build(Module module, Route route)
        {
            var upstream = string.IsNullOrWhiteSpace(route.Upstream) ? string.Empty : route.Upstream;
            if (!string.IsNullOrWhiteSpace(module.Path))
            {
                var modulePath = module.Path.EndsWith("/") ? module.Path : $"{module.Path}/";
                if (upstream.StartsWith("/"))
                {
                    upstream = upstream.Substring(1, upstream.Length - 1);
                }

                if (upstream.EndsWith("/"))
                {
                    upstream = upstream.Substring(0, upstream.Length - 1);
                }

                upstream = $"{modulePath}{upstream}";
            }

            if (string.IsNullOrWhiteSpace(upstream))
            {
                upstream = "/";
            }

            var routeInfo = string.Empty;
            switch (route.Use)
            {
                case "dispatcher":
                    routeInfo = $"dispatch a message to exchange: '{route.Exchange}'";
                    break;
                case "downstream":
                    routeInfo = $"call downstream: [{route.DownstreamMethod.ToUpperInvariant()}] '{route.Downstream}'";
                    break;
                case "return_value":
                    routeInfo = $"return a value: '{route.ReturnValue}'";
                    break;
            }

            var isProtectedInfo = _configuration.Auth is null || !_configuration.Auth.Global && route.Auth is null ||
                                  route.Auth == false
                ? "public"
                : "protected";
            _logger.LogInformation($"Added {isProtectedInfo} route for upstream: [{route.Method.ToUpperInvariant()}]" +
                                   $"'{upstream}' -> {routeInfo}");

            return upstream;
        }
    }
}