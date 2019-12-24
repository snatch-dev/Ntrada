using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ntrada.Configuration;
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
            var path = module.Path;
            var upstream = string.IsNullOrWhiteSpace(route.Upstream) ? string.Empty : route.Upstream;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var modulePath = path.EndsWith("/") ? path.Substring(0, path.Length - 1) : path;
                if (!upstream.StartsWith("/"))
                {
                    upstream = $"/{upstream}";
                }

                upstream = $"{modulePath}{upstream}";
            }

            if (upstream.EndsWith("/"))
            {
                upstream = upstream.Substring(0, upstream.Length - 1);
            }

            if (route.MatchAll)
            {
                upstream = $"{upstream}/{{*url}}";
            }

            var handler = _requestHandlerManager.Get(route.Use);
            var routeInfo = handler.GetInfo(route);
            var isPublicInfo = _options.Auth is null || !_options.Auth.Global && route.Auth is null ||
                               route.Auth == false
                ? "public"
                : "protected";

            var methods = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(route.Method))
            {
                methods.Add(route.Method.ToUpperInvariant());
            }

            if (route.Methods is {})
            {
                foreach (var method in route.Methods)
                {
                    if (string.IsNullOrWhiteSpace(method))
                    {
                        continue;
                    }

                    methods.Add(method.ToUpperInvariant());
                }
            }

            _logger.LogInformation($"Added {isPublicInfo} route for upstream: [{string.Join(", ", methods)}] " +
                                   $"'{upstream}' -> {routeInfo}");

            return upstream;
        }
    }
}