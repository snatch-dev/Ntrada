using System;

namespace Ntrada.Extensions.RabbitMq
{
    public class CorrelationContext : ICorrelationContext
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string ResourceId { get; set; }
        public string TraceId { get; set; }
        public string ConnectionId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}