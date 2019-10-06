using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace Ntrada.Extensions.RabbitMq.Clients
{
    internal sealed class RabbitMqClient : IRabbitMqClient
    {
        private const string EmptyContext = "{}";
        private readonly IConnection _connection;
        private readonly ILogger<RabbitMqClient> _logger;
        private readonly string _messageContextHeader;
        private readonly bool _contextEnabled;
        private readonly bool _loggerEnabled;

        public RabbitMqClient(IConnection connection, RabbitMqOptions options, ILogger<RabbitMqClient> logger)
        {
            _connection = connection;
            _logger = logger;
            _contextEnabled = options.Context?.Enabled == true;
            _messageContextHeader = string.IsNullOrWhiteSpace(options.Context?.Header)
                ? "message_context"
                : options.Context.Header;
            _loggerEnabled = options.Logger?.Enabled == true;
        }

        public void Send(object message, string routingKey, string exchange, object context = null)
        {
            using (var channel = _connection.CreateModel())
            {
                var json = JsonConvert.SerializeObject(message);
                var body = Encoding.UTF8.GetBytes(json);
                var properties = channel.CreateBasicProperties();
                var messageId = Guid.NewGuid().ToString("N");
                var correlationId = Guid.NewGuid().ToString("N");
                properties.MessageId = messageId;
                properties.CorrelationId = correlationId;
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                properties.Headers = new Dictionary<string, object>();
                if (_contextEnabled)
                {
                    IncludeContext(context, properties);
                }

                if (_loggerEnabled)
                {
                    _logger.LogInformation($"Sending a message with routing key: '{routingKey}' to the exchange: " +
                                           $"'{exchange}' [message id: '{messageId}', correlation id: '{correlationId}'].");
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

            properties.Headers.Add(_messageContextHeader, EmptyContext);
        }
    }
}