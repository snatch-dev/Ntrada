using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Ntrada.Extensions.Swagger
{
    public class SwaggerExtension : IExtension
    {
        public string Name => "swagger";
        public string Description => "Swagger Web API docs";

        public void Add(IServiceCollection services, IOptionsProvider optionsProvider)
        {
            var options = optionsProvider.GetForExtension<SwaggerOptions>(Name);
            services.AddSingleton(options);
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc(options.Name, new OpenApiInfo {Title = options.Title, Version = options.Version});
                c.DocumentFilter<WebApiDocumentFilter>();
                if (options.IncludeSecurity)
                {
                    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Description =
                            "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.ApiKey
                    });
                }
            });
        }

        public void Use(IApplicationBuilder app, IOptionsProvider optionsProvider)
        {
            var options = optionsProvider.GetForExtension<SwaggerOptions>(Name);
            var routePrefix = string.IsNullOrWhiteSpace(options.RoutePrefix) ? "swagger" : options.RoutePrefix;
            app.UseStaticFiles()
                .UseSwagger(c => c.RouteTemplate = routePrefix + "/{documentName}/swagger.json");

            if (options.ReDocEnabled)
            {
                app.UseReDoc(c =>
                {
                    c.RoutePrefix = routePrefix;
                    c.SpecUrl = $"{options.Name}/swagger.json";
                });

                return;
            }

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint($"/{routePrefix}/{options.Name}/swagger.json", options.Title);
                c.RoutePrefix = routePrefix;
            });
        }
    }
}