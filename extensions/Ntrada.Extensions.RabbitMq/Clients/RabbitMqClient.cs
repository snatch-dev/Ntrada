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
        private readonly bool _messageContextEnabled;
        private readonly bool _loggerEnabled;
        private readonly string _spanContextHeader;

        public RabbitMqClient(IConnection connection, RabbitMqOptions options, ILogger<RabbitMqClient> logger)
        {
            _connection = connection;
            _logger = logger;
            _messageContextEnabled = options.MessageContext?.Enabled == true;
            _messageContextHeader = string.IsNullOrWhiteSpace(options.MessageContext?.Header)
                ? "message_context"
                : options.MessageContext.Header;
            _loggerEnabled = options.Logger?.Enabled == true;
            _spanContextHeader = string.IsNullOrWhiteSpace(options.SpanContextHeader)
                ? "span_context"
                : options.SpanContextHeader;
        }

        public void Send(object message, string routingKey, string exchange, string messageId = null,
            string correlationId = null, string spanContext = null, object messageContext = null,
            IDictionary<string, object> headers = null)
        {
            using (var channel = _connection.CreateModel())
            {
                var json = JsonConvert.SerializeObject(message);
                var body = Encoding.UTF8.GetBytes(json);
                var properties = channel.CreateBasicProperties();
                properties.MessageId = string.IsNullOrWhiteSpace(messageId)
                    ? Guid.NewGuid().ToString("N")
                    : messageId;
                properties.CorrelationId = string.IsNullOrWhiteSpace(correlationId)
                    ? Guid.NewGuid().ToString("N")
                    : correlationId;
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                properties.Headers = new Dictionary<string, object>();
                if (_messageContextEnabled)
                {
                    IncludeMessageContext(messageContext, properties);
                }

                if (!string.IsNullOrWhiteSpace(spanContext))
                {
                    properties.Headers.Add(_spanContextHeader, spanContext);
                }

                if (!(headers is null))
                {
                    foreach (var (key, value) in headers)
                    {
                        properties.Headers.TryAdd(key, value);
                    }
                }

                if (_loggerEnabled)
                {
                    _logger.LogInformation($"Sending a message with routing key: '{routingKey}' to the exchange: " +
                                           $"'{exchange}' [message id: '{properties.MessageId}', correlation id: '{properties.CorrelationId}'].");
                }

                channel.BasicPublish(exchange, routingKey, properties, body);
            }
        }

        private void IncludeMessageContext(object context, IBasicProperties properties)
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