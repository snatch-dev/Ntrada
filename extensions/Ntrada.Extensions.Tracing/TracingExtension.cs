using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ntrada.Core;
using OpenTracing;
using OpenTracing.Util;

namespace Ntrada.Extensions.Tracing
{
    public class TracingExtension : IExtension
    {
        public string Name => "tracing";
        public string Description => "Open Tracing using Jaeger";
        
        public void Add(IServiceCollection services, IOptionsProvider optionsProvider)
        {
            var options = optionsProvider.GetForExtension<TracingOptions>("tracing");
            services.AddOpenTracing();
            services.AddSingleton(options);
            if (options.UseEmptyTracer)
            {
                var defaultTracer = DefaultTracer.Create();
                services.AddSingleton(defaultTracer);
                return;
            }

            services.AddSingleton<ITracer>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                var reporter = new RemoteReporter.Builder()
                    .WithSender(new UdpSender(options.UdpHost, options.UdpPort, options.MaxPacketSize))
                    .WithLoggerFactory(loggerFactory)
                    .Build();

                var sampler = GetSampler(options);

                var tracer = new Tracer.Builder(options.ServiceName)
                    .WithLoggerFactory(loggerFactory)
                    .WithReporter(reporter)
                    .WithSampler(sampler)
                    .Build();

                GlobalTracer.Register(tracer);

                return tracer;
            });
        }

        public void Use(IApplicationBuilder app, IOptionsProvider optionsProvider)
        {
            app.UseMiddleware<JaegerHttpMiddleware>();
        }

        private static ISampler GetSampler(TracingOptions options)
        {
            switch (options.Sampler)
            {
                case "const": return new ConstSampler(true);
                case "rate": return new RateLimitingSampler(options.MaxTracesPerSecond);
                case "probabilistic": return new ProbabilisticSampler(options.SamplingRate);
                default: return new ConstSampler(true);
            }
        }
    }
}