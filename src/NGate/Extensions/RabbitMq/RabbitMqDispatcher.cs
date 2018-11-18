using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NGate.Framework;
using RawRabbit;
using RawRabbit.Common;
using RawRabbit.Enrichers.MessageContext;
using RawRabbit.Instantiation;
using Route = NGate.Framework.Route;

namespace NGate.Extensions.RabbitMq
{
    public class RabbitMqDispatcher : IExtension
    {
        private readonly Configuration _configuration;
        private IBusClient _busClient;

        public string Name => "rabbitmq";

        public RabbitMqDispatcher(Configuration configuration)
        {
            _configuration = configuration;
        }

        public async Task InitAsync()
        {
            var extension = _configuration.Extensions.Single(e => e.Value.Use == Name).Value;
            var configurationPath = extension.Configuration;
            if (!File.Exists(configurationPath))
            {
                configurationPath = $"{_configuration.Config.SettingsPath}/{configurationPath}";
                if (!File.Exists(configurationPath))
                {
                    throw new Exception($"Configuration for an extension: '{Name}'," +
                                        $"was not found under: '{configurationPath}'.");
                }
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(configurationPath);

            var options = new RabbitMqOptions();
            builder.Build().Bind(options);

            _busClient = RawRabbitFactory.CreateInstanceFactory(new RawRabbitOptions
            {
                DependencyInjection = ioc => { ioc.AddSingleton(options); },
                Plugins = p => p
                    .UseAttributeRouting()
                    .UseRetryLater()
                    .UseMessageContext<CorrelationContext>()
                    .UseContextForwarding()
            }).Create();

            await Task.CompletedTask;
        }

        public async Task ExecuteAsync(ExecutionData executionData)
        {
            var message = executionData.Payload;
            var route = executionData.Route;
            var context = new CorrelationContext
            {
                Id = executionData.RequestId,
                Name = executionData.Route.RoutingKey,
                ResourceId = executionData.ResourceId,
                UserId = executionData.UserId,
                ConnectionId = executionData.Request.HttpContext.Connection.Id,
                CreatedAt = DateTime.UtcNow,
                TraceId = executionData.Request.HttpContext.TraceIdentifier
            };
            await _busClient.PublishAsync(message, ctx => ctx.UseMessageContext(context)
                .UsePublishConfiguration(c =>
                    c.OnDeclaredExchange(e => e.WithName(route.Exchange)).WithRoutingKey(route.RoutingKey)));
        }

        public async Task CloseAsync()
        {
            _busClient = null;
            await Task.CompletedTask;
        }
    }
}