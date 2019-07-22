using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Ntrada.Handlers.RabbitMq;

namespace Ntrada.Samples.Api
{
    public static class Program
    {
        public static async Task Main(string[] args)
            => await WebHost.CreateDefaultBuilder(args)
                .ConfigureServices(services => services
                    .AddOpenTracing()
                    .AddSingleton<IContextBuilder, CorrelationContextBuilder>()
                    .AddRabbitMq<CorrelationContext>()
                    .AddNtrada())
                .Configure(app => app
                    .UseRabbitMq()
                    .UseNtrada())
                .Build()
                .RunAsync();
    }
}