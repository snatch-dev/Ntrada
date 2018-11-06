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
        private IBusClient _busClient;

        public string Name => "rabbitmq_dispatcher";

        public async Task InitAsync(Configuration configuration)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            var options = new RabbitMqOptions();
            builder.Build().GetSection("rabbitmq").Bind(options);

            _busClient = RawRabbitFactory.CreateInstanceFactory(new RawRabbitOptions
            {
                DependencyInjection = ioc =>
                {
                    ioc.AddSingleton(options);
                    ioc.AddSingleton(configuration);
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
            var message = executionData.Payload;
            var route = executionData.Route;
            var context = new CorrelationContext
            {
                Id = Guid.NewGuid().ToString(),
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