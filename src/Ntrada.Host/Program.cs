using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Ntrada.Host
{
    [ExcludeFromCodeCoverage]
    public static class Program
    {
        public static Task Main(string[] args)
            => CreateHostBuilder(args).Build().RunAsync();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureAppConfiguration(builder =>
                        {
                            var configPath = args?.FirstOrDefault() ?? "ntrada.yml";
                            builder.AddYamlFile(configPath, false);
                        })
                        .ConfigureServices(services => services.AddNtrada())
                        .Configure(app => app.UseNtrada());
                });
    }
}