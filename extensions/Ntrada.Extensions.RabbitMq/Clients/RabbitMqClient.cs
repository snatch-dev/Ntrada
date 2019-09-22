using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace Ntrada.Extensions.RabbitMq.Clients
{
    internal sealed class RabbitMqClient : IRabbitMqClient
    {
        private const string EmptyContext = "{}";
        private readonly IConnection _connection;
        private readonly RabbitMqOptions _options;
        private readonly string _messageContextHeader;
        private readonly bool _contextEnabled;
        private readonly bool _includeCorrelationId;

        public RabbitMqClient(IConnection connection, RabbitMqOptions options)
        {
            _connection = connection;
            _options = options;
            _contextEnabled = options.Context?.Enabled == true;
            _messageContextHeader = string.IsNullOrWhiteSpace(options.Context?.Header)
                ? "message_context"
                : options.Context.Header;
            _includeCorrelationId = options.Context?.IncludeCorrelationId == true;
        }

        public void Send(object message, string routingKey, string exchange, object context = null)
        {
            using (var channel = _connection.CreateModel())
            {
                var json = JsonConvert.SerializeObject(message);
                var body = Encoding.UTF8.GetBytes(json);
                var properties = channel.CreateBasicProperties();
                properties.MessageId = Guid.NewGuid().ToString("N");
                properties.CorrelationId = Guid.NewGuid().ToString("N");
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                properties.Headers = new Dictionary<string, object>();
                if (_contextEnabled)
                {
                    IncludeContext(context, properties);
                }

                if (_options.Exchange.DeclareExchange)
                {
                    channel.ExchangeDeclare(exchange, _options.Exchange.Type, _options.Exchange.Durable,
                        _options.Exchange.AutoDelete);
                }

                channel.BasicPublish(exchange, routingKey, properties, body);
            }
        }

        private void IncludeContext(object context, IBasicProperties properties)
        {
            if (!(context is null))
            {
                properties.Headers.Add(_messageContextHeader, JsonConvert.SerializeObject(context));
                return;
            }

            if (_includeCorrelationId)
            {
                properties.Headers.Add(_messageContextHeader,
                    JsonConvert.SerializeObject(new Context(properties.CorrelationId)));
                return;
            }
            
            properties.Headers.Add(_messageContextHeader, EmptyContext);
        }

        private class Context
        {
            public string CorrelationId { get; set; }

            public Context(string correlationId)
            {
                CorrelationId = correlationId;
            }
        }
    }
}