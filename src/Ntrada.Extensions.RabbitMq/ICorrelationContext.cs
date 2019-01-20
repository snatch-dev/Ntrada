using System;

namespace Ntrada.Extensions.RabbitMq
{
    public interface ICorrelationContext
    {
        string Id { get; }
        string UserId { get; }
        string ResourceId { get; }
        string TraceId { get; }
        string ConnectionId { get; }
        string Name { get; }
        DateTime CreatedAt { get; }
    }
}