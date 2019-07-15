using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ntrada.Configuration;
using Ntrada.Models;
using OpenTracing;
using RawRabbit;
using RawRabbit.Configuration;
using RawRabbit.Enrichers.MessageContext;
using RawRabbit.Instantiation;

namespace Ntrada.Extensions.RabbitMq
{
    public class RabbitMqDispatcher : IDispatcherExtension
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly NtradaConfiguration _configuration;
        private IBusClient _busClient;

        public string Name => "rabbitmq";

        public RabbitMqDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _configuration = _serviceProvider.GetService<NtradaConfiguration>();
        }

        public async Task InitAsync()
        {
            var configuration = _serviceProvider.GetService<IConfiguration>();
            var options = new RabbitMqOptions();
            configuration.GetSection("rabbitMq").Bind(options);
            _busClient = RawRabbitFactory.CreateInstanceFactory(new RawRabbitOptions
            {
                DependencyInjection = ioc =>
                {
                    ioc.AddSingleton(options);
                    ioc.AddSingleton<RawRabbitConfiguration>(options);
                },
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
            var spanContext = string.Empty;
            if (_configuration.UseJaeger)
            {
                var tracer = _serviceProvider.GetService<ITracer>();
                spanContext = tracer is null ? string.Empty : tracer.ActiveSpan.Context.ToString();
            }

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
                TraceId = executionData.Request.HttpContext.TraceIdentifier,
                SpanContext = spanContext
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