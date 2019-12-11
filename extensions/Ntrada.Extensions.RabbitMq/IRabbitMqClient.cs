using System.Collections.Generic;

namespace Ntrada.Extensions.RabbitMq
{
    public interface IRabbitMqClient
    {
        void Send(object message, string routingKey, string exchange, string messageId = null,
            string correlationId = null, string spanContext = null, object messageContext = null,
            IDictionary<string, object> headers = null, string userId = null);
    }
}