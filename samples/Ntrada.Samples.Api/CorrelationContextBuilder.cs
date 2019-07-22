using System;
using Microsoft.Extensions.DependencyInjection;
using Ntrada.Handlers.RabbitMq;
using OpenTracing;

namespace Ntrada.Samples.Api
{
    public class CorrelationContextBuilder : IContextBuilder
    {
        private readonly NtradaConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        public CorrelationContextBuilder(NtradaConfiguration configuration, IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        public object Build(ExecutionData executionData)
        {
            var spanContext = string.Empty;
            if (_configuration.UseJaeger)
            {
                var tracer = _serviceProvider.GetService<ITracer>();
                spanContext = tracer is null ? string.Empty :
                    tracer.ActiveSpan is null ? string.Empty : tracer.ActiveSpan.Context.ToString();
            }

            return new CorrelationContext
            {
                Id = executionData.RequestId,
                UserId = executionData.UserId,
                ResourceId = executionData.ResourceId,
                TraceId = executionData.TraceId,
                ConnectionId = executionData.Request.HttpContext.Connection.Id,
                Name = executionData.Route.Config["routing_key"],
                CreatedAt = DateTime.UtcNow,
                SpanContext = spanContext
            };
        }
    }
}