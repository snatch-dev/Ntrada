using System;

namespace Ntrada.Samples.Api
{
    public class CorrelationContext
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string ResourceId { get; set; }
        public string TraceId { get; set; }
        public string ConnectionId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public string SpanContext { get; set; }
    }
}