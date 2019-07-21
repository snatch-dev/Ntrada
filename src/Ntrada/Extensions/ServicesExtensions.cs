using Microsoft.Extensions.DependencyInjection;
using Ntrada.Auth;
using Ntrada.Handlers;
using Ntrada.Requests;
using Ntrada.Routing;

namespace Ntrada.Extensions
{
    public static class ServicesExtensions
    {
        public static IServiceCollection AddNtrada(this IServiceCollection services)
        {
            services.AddSingleton<IAccessValidator, AccessValidator>();
            services.AddSingleton<IDownstreamBuilder, DownstreamBuilder>();
            services.AddSingleton<IExtensionManager, ExtensionManager>();
            services.AddSingleton<INtradaBuilder, NtradaBuilder>();
            services.AddSingleton<IPayloadBuilder, PayloadBuilder>();
            services.AddSingleton<IPayloadManager, PayloadManager>();
            services.AddSingleton<IPayloadValidator, PayloadValidator>();
            services.AddSingleton<IRequestExecutionValidator, RequestExecutionValidator>();
            services.AddSingleton<IRequestHandlerManager, RequestHandlerManager>();
            services.AddSingleton<IRequestProcessor, RequestProcessor>();
            services.AddSingleton<IRouteConfigurator, RouteConfigurator>();
            services.AddSingleton<IRouteProvider, RouteProvider>();
            services.AddSingleton<ISchemaValidator, SchemaValidator>();
            services.AddSingleton<IUpstreamBuilder, UpstreamBuilder>();
            services.AddSingleton<IValueProvider, ValueProvider>();
            services.AddSingleton<DispatcherHandler>();
            services.AddSingleton<DownstreamHandler>();
            services.AddSingleton<ReturnValueHandler>();

            return services;
        }
    }
}