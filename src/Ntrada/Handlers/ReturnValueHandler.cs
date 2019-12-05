using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Ntrada.Hooks;
using Route = Ntrada.Configuration.Route;

namespace Ntrada.Handlers
{
    internal sealed class ReturnValueHandler : IHandler
    {
        private readonly IRequestProcessor _requestProcessor;
        private readonly IEnumerable<IRequestHook> _requestHooks;
        private readonly IEnumerable<IResponseHook> _responseHooks;

        public ReturnValueHandler(IRequestProcessor requestProcessor, IServiceProvider serviceProvider)
        {
            _requestProcessor = requestProcessor;
            _requestHooks = serviceProvider.GetServices<IRequestHook>();
            _responseHooks = serviceProvider.GetServices<IResponseHook>();
        }

        public string GetInfo(Route route) => $"return a value: '{route.ReturnValue}'";

        public async Task HandleAsync(HttpContext context, RouteConfig config)
        {
            var executionData = await _requestProcessor.ProcessAsync(config, context);
            if (_requestHooks is {})
            {
                foreach (var hook in _requestHooks)
                {
                    if (hook is null)
                    {
                        continue;
                    }

                    await hook.InvokeAsync(context.Request, executionData);
                }
            }

            if (_responseHooks is {})
            {
                foreach (var hook in _responseHooks)
                {
                    if (hook is null)
                    {
                        continue;
                    }

                    await hook.InvokeAsync(context.Response, executionData);
                }
            }

            await context.Response.WriteAsync(config.Route?.ReturnValue ?? string.Empty);
        }
    }
}