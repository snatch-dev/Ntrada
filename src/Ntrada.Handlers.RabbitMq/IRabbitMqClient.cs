using System.Threading.Tasks;

namespace Ntrada.Handlers.RabbitMq
{
    public interface IRabbitMqClient
    {
        Task SendAsync(object message, string routingKey, string exchange, object context = null);
    }
}