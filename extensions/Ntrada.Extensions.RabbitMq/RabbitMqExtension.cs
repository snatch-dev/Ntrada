using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Ntrada.Extensions.RabbitMq.Clients;
using Ntrada.Extensions.RabbitMq.Contexts;
using Ntrada.Extensions.RabbitMq.Handlers;
using RabbitMQ.Client;

namespace Ntrada.Extensions.RabbitMq
{
    public class RabbitMqExtension : IExtension
    {
        public string Name => "rabbitmq";
        public string Description => "RabbitMQ message broker";

        public void Add(IServiceCollection services, IOptionsProvider optionsProvider)
        {
            var options = optionsProvider.GetForExtension<RabbitMqOptions>(Name);
            services.AddSingleton(options);
            services.AddSingleton(sp =>
            {
                var connectionFactory = new ConnectionFactory
                {
                    HostName = options.HostName,
                    Port = options.Port,
                    VirtualHost = options.VirtualHost,
                    UserName = options.Username,
                    Password = options.Password,
                    RequestedConnectionTimeout = options.RequestedConnectionTimeout,
                    SocketReadTimeout = options.SocketReadTimeout,
                    SocketWriteTimeout = options.SocketWriteTimeout,
                    RequestedChannelMax = options.RequestedChannelMax,
                    RequestedFrameMax = options.RequestedFrameMax,
                    RequestedHeartbeat = options.RequestedHeartbeat,
                    UseBackgroundThreadsForIO = options.UseBackgroundThreadsForIO,
                    Ssl = options.Ssl is null
                        ? new SslOption()
                        : new SslOption(options.Ssl.ServerName, options.Ssl.CertificatePath, options.Ssl.Enabled),
                };

                return connectionFactory.CreateConnection();
            });

            services.AddTransient<IRabbitMqClient, RabbitMqClient>();
            services.AddTransient<RabbitMqHandler>();
            var contextEnabled = options.Context?.Enabled == true;
            if (!contextEnabled)
            {
                services.AddSingleton<IContextBuilder, NullContextBuilder>();
                return;
            }

            var customContext = options.Context.Custom;
            if (customContext)
            {
                return;
            }

            services.AddSingleton<IContextBuilder, NullContextBuilder>();
        }

        public void Use(IApplicationBuilder app, IOptionsProvider optionsProvider)
        {
            app.UseRequestHandler<RabbitMqHandler>(Name);
        }
    }
}