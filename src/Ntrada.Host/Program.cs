using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace Ntrada.Host
{
    public static class Program
    {
        public static async Task Main(string[] args)
            => await WebHost.CreateDefaultBuilder(args)
                .ConfigureServices(services => services.AddNtrada())
                .Configure(app => app.UseNtrada())
                .Build()
                .RunAsync();
    }
}