using System.Threading.Tasks;
using RawRabbit;
using RawRabbit.Enrichers.MessageContext;

namespace Ntrada.Handlers.RabbitMq
{
    public class RabbitMqClient : IRabbitMqClient
    {
        private readonly IBusClient _busClient;

        public RabbitMqClient(IBusClient busClient)
        {
            _busClient = busClient;
        }

        public async Task SendAsync(object message, string routingKey, string exchange, object context = null)
        {
            if (context is null)
            {
                await _busClient.PublishAsync(message, ctx => ctx.UsePublishConfiguration(c =>
                    c.OnDeclaredExchange(e => e.WithName(exchange)).WithRoutingKey(routingKey)));
                return;
            }

            await _busClient.PublishAsync(message, ctx => ctx.UseMessageContext(context)
                .UsePublishConfiguration(c =>
                    c.OnDeclaredExchange(e => e.WithName(exchange)).WithRoutingKey(routingKey)));
        }
    }
}