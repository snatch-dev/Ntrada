using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Ntrada.Samples.Api
{
    [ExcludeFromCodeCoverage]
    public class Program
    {
        public static Task Main(string[] args)
            => CreateHostBuilder(args).Build().RunAsync();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureAppConfiguration(builder =>
                    {
                        const string extension = "yml";
                        var ntradaConfig = Environment.GetEnvironmentVariable("NTRADA_CONFIG");
                        var configPath = args?.FirstOrDefault() ?? ntradaConfig ?? $"ntrada.{extension}";
                        if (!configPath.EndsWith($".{extension}"))
                        {
                            configPath += $".{extension}";
                        }

                        builder.AddYamlFile(configPath, false);
                    }).UseStartup<Startup>();
                });
    }
}