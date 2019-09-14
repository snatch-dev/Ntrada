using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace Ntrada.Extensions.RabbitMq.Clients
{
    internal sealed class RabbitMqClient : IRabbitMqClient
    {
        private readonly IConnection _connection;
        private readonly RabbitMqOptions _options;
        private const string MessageContextHeader = "message_context";

        public RabbitMqClient(IConnection connection, RabbitMqOptions options)
        {
            _connection = connection;
            _options = options;
        }

        public void Send(object message, string routingKey, string exchange, object context = null)
        {
            using (var channel = _connection.CreateModel())
            {
                var json = JsonConvert.SerializeObject(message);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = channel.CreateBasicProperties();

                if (!(context is null))
                {
                    properties.Headers.Add(MessageContextHeader, JsonConvert.SerializeObject(context));
                }

                if (_options.Exchange.DeclareExchange)
                {
                    channel.ExchangeDeclare(exchange, _options.Exchange.Type, _options.Exchange.Durable, 
                        _options.Exchange.AutoDelete);
                }
                
                channel.BasicPublish(exchange, routingKey, properties, body);
            }
        }
    }
}