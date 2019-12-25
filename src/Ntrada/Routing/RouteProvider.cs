using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Ntrada.Options;
using Ntrada.WebApi;

namespace Ntrada.Routing
{
    internal sealed class RouteProvider : IRouteProvider
    {
        private readonly IDictionary<string, Action<IEndpointRouteBuilder, string, RouteConfig>> _methods;
        private readonly IRouteConfigurator _routeConfigurator;
        private readonly IRequestExecutionValidator _requestExecutionValidator;
        private readonly IUpstreamBuilder _upstreamBuilder;
        private readonly WebApiEndpointDefinitions _definitions;
        private readonly NtradaOptions _options;
        private readonly IRequestHandlerManager _requestHandlerManager;
        private readonly ILogger<RouteProvider> _logger;

        public RouteProvider(NtradaOptions options, IRequestHandlerManager requestHandlerManager,
            IRouteConfigurator routeConfigurator, IRequestExecutionValidator requestExecutionValidator,
            IUpstreamBuilder upstreamBuilder, WebApiEndpointDefinitions definitions, ILogger<RouteProvider> logger)
        {
            _routeConfigurator = routeConfigurator;
            _requestExecutionValidator = requestExecutionValidator;
            _upstreamBuilder = upstreamBuilder;
            _definitions = definitions;
            _options = options;
            _requestHandlerManager = requestHandlerManager;
            _logger = logger;
            _methods = new Dictionary<string, Action<IEndpointRouteBuilder, string, RouteConfig>>
            {
                ["get"] = (builder, path, routeConfig) =>
                    builder.MapGet(path, ctx => Handle(ctx, routeConfig)),
                ["post"] = (builder, path, routeConfig) =>
                    builder.MapPost(path, ctx => Handle(ctx, routeConfig)),
                ["put"] = (builder, path, routeConfig) =>
                    builder.MapPut(path, ctx => Handle(ctx, routeConfig)),
                ["delete"] = (builder, path, routeConfig) =>
                    builder.MapDelete(path, ctx => Handle(ctx, routeConfig)),
            };
        }

        private async Task Handle(HttpContext context, RouteConfig routeConfig)
        {
            var skipAuth = _options.Auth is null ||
                           !_options.Auth.Global && routeConfig.Route.Auth is null ||
                           routeConfig.Route.Auth == false;
            if (!skipAuth &&
                !await _requestExecutionValidator.TryExecuteAsync(context, routeConfig))
            {
                return;
            }

            var handler = routeConfig.Route.Use;
            await _requestHandlerManager.HandleAsync(handler, context, routeConfig);
        }

        public Action<IEndpointRouteBuilder> Build() => routeBuilder =>
        {
            foreach (var module in _options.Modules.Where(m => m.Value.Enabled != false))
            {
                _logger.LogInformation($"Building routes for the module: '{module.Key}'");
                foreach (var route in module.Value.Routes)
                {
                    if (string.IsNullOrWhiteSpace(route.Method) && route.Methods is null)
                    {
                        throw new ArgumentException("Both, route 'method' and 'methods' cannot be empty.");
                    }

                    route.Upstream = _upstreamBuilder.Build(module.Value, route);
                    var routeConfig = _routeConfigurator.Configure(module.Value, route);

                    if (!string.IsNullOrWhiteSpace(route.Method))
                    {
                        _methods[route.Method](routeBuilder, route.Upstream, routeConfig);
                        AddEndpointDefinition(route.Method, route.Upstream);
                    }

                    if (route.Methods is null)
                    {
                        continue;
                    }

                    foreach (var method in route.Methods)
                    {
                        var methodType = method.ToLowerInvariant();
                        _methods[methodType](routeBuilder, route.Upstream, routeConfig);
                        AddEndpointDefinition(methodType, route.Upstream);
                    }
                }
            }
        };
        
        private void AddEndpointDefinition(string method, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                path = "/";
            }
            
            _definitions.Add(new WebApiEndpointDefinition
            {
                Method = method,
                Path = path,
                Responses = new List<WebApiEndpointResponse>
                {
                    new WebApiEndpointResponse
                    {
                        StatusCode = 200
                    }
                }
            });
        }
    }
}