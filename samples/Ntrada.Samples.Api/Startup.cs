using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Ntrada.Samples.Api
{
    [ExcludeFromCodeCoverage]
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddNtrada();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseNtrada();
        }
    }
}