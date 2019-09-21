using Microsoft.Extensions.Logging;
using Ntrada.Core.Configuration;
using Ntrada.Options;

namespace Ntrada.Routing
{
    internal sealed class UpstreamBuilder : IUpstreamBuilder
    {
        private readonly NtradaOptions _options;
        private readonly IRequestHandlerManager _requestHandlerManager;
        private readonly ILogger<UpstreamBuilder> _logger;

        public UpstreamBuilder(NtradaOptions options, IRequestHandlerManager requestHandlerManager,
            ILogger<UpstreamBuilder> logger)
        {
            _options = options;
            _requestHandlerManager = requestHandlerManager;
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

            var handler = _requestHandlerManager.Get(route.Use);
            var routeInfo = handler.GetInfo(route);
            var isPublicInfo = _options.Auth is null || !_options.Auth.Global && route.Auth is null ||
                               route.Auth == false
                ? "public"
                : "protected";
            _logger.LogInformation($"Added {isPublicInfo} route for upstream: [{route.Method.ToUpperInvariant()}]" +
                                   $"'{upstream}' -> {routeInfo}");

            return upstream;
        }
    }
}