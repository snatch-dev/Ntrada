using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Ntrada.Core;

namespace Ntrada.Extensions.CustomErrors
{
    public class CustomErrorsExtension : IExtension
    {
        public string Name => "customErrors";
        public string Description => "Custom errors handler";
        
        public void Add(IServiceCollection services, IOptionsProvider optionsProvider)
        {
            var options = optionsProvider.GetForExtension<CustomErrorsOptions>(Name);
            services.AddSingleton(options);
            services.AddScoped<ErrorHandlerMiddleware>();
        }

        public void Use(IApplicationBuilder app, IOptionsProvider optionsProvider)
        {
            app.UseMiddleware<ErrorHandlerMiddleware>();
        }
    }
}