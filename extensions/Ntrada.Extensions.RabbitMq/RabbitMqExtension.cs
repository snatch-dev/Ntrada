using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Ntrada.Core;
using RawRabbit;
using RawRabbit.Configuration;
using RawRabbit.Enrichers.MessageContext;
using RawRabbit.Instantiation;

namespace Ntrada.Extensions.RabbitMq
{
    public class RabbitMqExtension : IExtension
    {
        public string Name => "rabbitmq";
        public string Description => "RabbitMQ message broker";
        public void Add(IServiceCollection services, IOptionsProvider optionsProvider)
        {
            var options = optionsProvider.GetForExtension<RabbitMqOptions>(Name);
            services.AddTransient<IRabbitMqClient, RabbitMqClient>();
            services.AddTransient<RabbitMqHandler>();
//            var hasContext = typeof(TContext) != typeof(NullContext);
            var hasContext = false;
            if (!hasContext)
            {
                services.AddSingleton<IContextBuilder, NullContextBuilder>();
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
//                    if (typeof(TContext) != typeof(NullContext))
//                    {
//                        plugins.UseMessageContext<TContext>();
//                    }
                }
            }).Create();
            services.AddSingleton(busClient);
        }

        public void Use(IApplicationBuilder app, IOptionsProvider optionsProvider)
        {
            app.UseRequestHandler<RabbitMqHandler>(Name);
        }
    }
}