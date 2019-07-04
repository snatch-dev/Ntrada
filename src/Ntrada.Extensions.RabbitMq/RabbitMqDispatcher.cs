using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Ntrada.Configuration;
using Ntrada.Models;
using RawRabbit;
using RawRabbit.Enrichers.MessageContext;
using RawRabbit.Instantiation;

namespace Ntrada.Extensions.RabbitMq
{
    public class RabbitMqDispatcher : IDispatcherExtension
    {
        private readonly NtradaConfiguration _configuration;
        private IBusClient _busClient;

        public string Name => "rabbitmq";

        public RabbitMqDispatcher(NtradaConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task InitAsync()
        {
            var extension = _configuration.Extensions.Single(e => e.Value.Use == Name).Value;
            var settings = extension.Settings;
            if (!File.Exists(settings))
            {
                settings = $"{_configuration.SettingsPath}/{settings}.json";
                if (!File.Exists(settings))
                {
                    throw new Exception($"Configuration for an extension: '{Name}'," +
                                        $"was not found under: '{settings}.json'.");
                }
            }

            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.ToLowerInvariant();
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(settings)
                .AddJsonFile($"{settings}.{environment}.json", optional: true)
                .AddEnvironmentVariables();;

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