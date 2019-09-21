using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Ntrada.Extensions.Cors
{
    public class CorsExtension : IExtension
    {
        public string Name => "cors";
        public string Description => "Cross-Origin Resource Sharing";
        
        public void Add(IServiceCollection services, IOptionsProvider optionsProvider)
        {
            var options = optionsProvider.GetForExtension<CorsOptions>(Name);
            services.AddCors(cors =>
            {
                var allowedHeaders = options.AllowedHeaders ?? Enumerable.Empty<string>();
                var allowedMethods = options.AllowedMethods ?? Enumerable.Empty<string>();
                var allowedOrigins = options.AllowedOrigins ?? Enumerable.Empty<string>();
                var exposedHeaders = options.ExposedHeaders ?? Enumerable.Empty<string>();
                cors.AddPolicy("CorsPolicy", builder =>
                {
                    if (options.AllowCredentials)
                    {
                        builder.AllowCredentials();
                    }
                    else
                    {
                        builder.DisallowCredentials();
                    }
                    
                    builder.WithHeaders(allowedHeaders.ToArray())
                        .WithMethods(allowedMethods.ToArray())
                        .WithOrigins(allowedOrigins.ToArray())
                        .WithExposedHeaders(exposedHeaders.ToArray());
                });
            });
        }

        public void Use(IApplicationBuilder app, IOptionsProvider optionsProvider)
        {
            app.UseCors("CorsPolicy");
        }
    }
}