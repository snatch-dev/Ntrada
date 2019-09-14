using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace Ntrada.Extensions.RabbitMq.Clients
{
    internal sealed class RabbitMqClient : IRabbitMqClient
    {
        private readonly IConnection _connection;
        private const string MessageContextHeader = "message_context";

        public RabbitMqClient(IConnection connection)
            => _connection = connection;

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
                
                channel.BasicPublish(exchange, routingKey, properties, body);
            }
        }
    }
}