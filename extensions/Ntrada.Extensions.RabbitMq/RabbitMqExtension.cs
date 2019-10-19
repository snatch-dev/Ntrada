using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ntrada.Extensions.RabbitMq.Clients;
using Ntrada.Extensions.RabbitMq.Contexts;
using Ntrada.Extensions.RabbitMq.Handlers;
using Ntrada.Options;
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
                    HostName = options.Hostnames?.FirstOrDefault(),
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

                var connection = connectionFactory.CreateConnection(options.ConnectionName);
                if (options.Exchange?.DeclareExchange != true)
                {
                    return connection;
                }

                var ntradaOptions = optionsProvider.Get<NtradaOptions>();
                var exchanges = ntradaOptions.Modules
                    .SelectMany(m => m.Value.Routes)
                    .Where(m => m.Use.Equals(Name, StringComparison.InvariantCultureIgnoreCase))
                    .SelectMany(r => r.Config)
                    .Where(c => c.Key.Equals("exchange", StringComparison.InvariantCultureIgnoreCase))
                    .Distinct()
                    .ToList();

                if (!exchanges.Any())
                {
                    return connection;
                }

                var logger = sp.GetService<ILogger<IConnection>>();
                var loggerEnabled = options.Logger?.Enabled == true;

                using (var channel = connection.CreateModel())
                {
                    foreach (var exchange in exchanges)
                    {
                        var name = exchange.Value;
                        var type = options.Exchange.Type;
                        if (loggerEnabled)
                        {
                            logger.LogInformation($"Declaring an exchange: '{name}', type: '{type}'.");
                        }

                        channel.ExchangeDeclare(name, type, options.Exchange.Durable, options.Exchange.AutoDelete);
                    }
                }

                return connection;
            });

            services.AddTransient<IRabbitMqClient, RabbitMqClient>();
            services.AddTransient<RabbitMqHandler>();
            services.AddSingleton<IContextBuilder, NullContextBuilder>();
            services.AddSingleton<ISpanContextBuilder, NullSpanContextBuilder>();
        }

        public void Use(IApplicationBuilder app, IOptionsProvider optionsProvider)
        {
            app.UseRequestHandler<RabbitMqHandler>(Name);
        }
    }
}