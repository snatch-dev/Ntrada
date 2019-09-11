using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Ntrada.Host
{
    public static class Program
    {
        public static async Task Main(string[] args)
            => await WebHost.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder =>
                {
                    var configPath = args?.FirstOrDefault() ?? "ntrada.yml";
                    builder.AddYamlFile(configPath, false);
                })
                .ConfigureServices(services => services.AddNtrada())
                .Configure(app => app.UseNtrada())
                .Build()
                .RunAsync();
    }
}