using System.Threading.Tasks;

namespace Ntrada.Extensions.RabbitMq
{
    public interface IRabbitMqClient
    {
        void Send(object message, string routingKey, string exchange, object context = null);
    }
}