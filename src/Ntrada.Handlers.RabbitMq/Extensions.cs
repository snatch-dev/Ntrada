using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RawRabbit;
using RawRabbit.Configuration;
using RawRabbit.Enrichers.MessageContext;
using RawRabbit.Instantiation;

namespace Ntrada.Handlers.RabbitMq
{
    public static class Extensions
    {
        private const string HandlerName = "message_broker";
        private const string SectionName = "rabbitMq";

        public static IServiceCollection AddRabbitMq(this IServiceCollection services, string sectionName = SectionName)
            => services.AddRabbitMq<NullContext>(sectionName);

        public static IServiceCollection AddRabbitMq<TContext>(this IServiceCollection services,
            string sectionName = SectionName) where TContext : class, new()
        {
            services.AddTransient<IRabbitMqClient, RabbitMqClient>();
            services.AddTransient<RabbitMqHandler>();
            var hasContext = typeof(TContext) != typeof(NullContext);
            if (!hasContext)
            {
                services.AddSingleton<IContextBuilder, NullContextBuilder>();
            }

            var options = new RabbitMqOptions();
            using (var scope = services.BuildServiceProvider())
            {
                var configuration = scope.GetService<IConfiguration>();
                configuration.GetSection(sectionName).Bind(options);
            }

            services.AddSingleton(options);
            var busClient = RawRabbitFactory.CreateInstanceFactory(new RawRabbitOptions
            {
                DependencyInjection = ioc =>
                {
                    ioc.AddSingleton(options);
                    ioc.AddSingleton<RawRabbitConfiguration>(options);
                },
                Plugins = plugins =>
                {
                    plugins
                        .UseAttributeRouting()
                        .UseRetryLater()
                        .UseContextForwarding();
                    if (typeof(TContext) != typeof(NullContext))
                    {
                        plugins.UseMessageContext<TContext>();
                    }
                }
            }).Create();
            services.AddSingleton(busClient);

            return services;
        }

        public static IApplicationBuilder UseRabbitMq(this IApplicationBuilder app, string handlerName = HandlerName)
            => app.UseRequestHandler<RabbitMqHandler>(handlerName);
    }
}