using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Ntrada.Auth;
using Ntrada.Configuration;
using Ntrada.Extensions;
using Ntrada.Handlers;
using Ntrada.Options;
using Ntrada.Requests;
using Ntrada.Routing;
using Polly;

[assembly: InternalsVisibleTo("Ntrada.Tests.Unit")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
namespace Ntrada
{
    public static class NtradaExtensions
    {
        private static readonly string Logo = @"
    / | / / /__________ _____/ /___ _
   /  |/ / __/ ___/ __ `/ __  / __ `/
  / /|  / /_/ /  / /_/ / /_/ / /_/ / 
 /_/ |_/\__/_/   \__,_/\__,_/\__,_/ 


 /___ API Gateway (Entrance) ___/";


        public static IServiceCollection AddNtrada(this IServiceCollection services)
        {
            var (configuration, optionsProvider) = BuildConfiguration(services);
            
            return services.AddCoreServices()
                .ConfigureLogging(configuration)
                .ConfigureHttpClient(configuration)
                .ConfigurePayloads(configuration)
                .AddNtradaServices()
                .AddExtensions(optionsProvider);
        }

        private static (NtradaOptions, OptionsProvider) BuildConfiguration(IServiceCollection services)
        {
            IConfiguration config;
            using (var scope = services.BuildServiceProvider().CreateScope())
            {
                config = scope.ServiceProvider.GetService<IConfiguration>();
            }
            
            var optionsProvider = new OptionsProvider(config);
            services.AddSingleton<IOptionsProvider>(optionsProvider);
            var configuration = optionsProvider.Get<NtradaOptions>();
            services.AddSingleton(configuration);

            return (configuration, optionsProvider);
        }

        private static IServiceCollection AddCoreServices(this IServiceCollection services)
        {
            services.AddMvcCore()
                .AddJsonFormatters()
                .AddJsonOptions(o => o.SerializerSettings.Formatting = Formatting.Indented);
            
            return services;
        }
        
        private static IServiceCollection ConfigureLogging(this IServiceCollection services, NtradaOptions options)
        {
            services.AddLogging(
                builder =>
                {
                    builder.AddFilter("Microsoft", LogLevel.Warning)
                        .AddFilter("System", LogLevel.Warning)
                        .AddConsole();
                });
            
            return services;
        }

        private static IServiceCollection ConfigureHttpClient(this IServiceCollection services, NtradaOptions options)
        {
            var http = options.Http ?? new Http();
            var httpClientBuilder = services.AddHttpClient("ntrada");
            httpClientBuilder.AddTransientHttpErrorPolicy(p =>
                p.WaitAndRetryAsync(http.Retries, retryAttempt =>
                {
                    var interval = http.Exponential
                        ? Math.Pow(http.Interval, retryAttempt)
                        : http.Interval;

                    return TimeSpan.FromSeconds(interval);
                }));
            
            return services;
        }

        private static IServiceCollection ConfigurePayloads(this IServiceCollection services, NtradaOptions options)
        {
            if (options.PayloadsFolder is null)
            {
                options.PayloadsFolder = "Payloads";
            }

            if (options.PayloadsFolder.EndsWith("/"))
            {
                options.PayloadsFolder = options.PayloadsFolder
                    .Substring(0, options.PayloadsFolder.Length - 1);
            }
            
            return services;
        }

        private static IServiceCollection AddNtradaServices(this IServiceCollection services)
        {
            services.AddSingleton<IAuthenticationManager, AuthenticationManager>();
            services.AddSingleton<IAuthorizationManager, AuthorizationManager>();
            services.AddSingleton<IPolicyManager, PolicyManager>();
            services.AddSingleton<IDownstreamBuilder, DownstreamBuilder>();
            services.AddSingleton<IPayloadBuilder, PayloadBuilder>();
            services.AddSingleton<IPayloadManager, PayloadManager>();
            services.AddSingleton<IPayloadTransformer, PayloadTransformer>();
            services.AddSingleton<IPayloadValidator, PayloadValidator>();
            services.AddSingleton<IRequestExecutionValidator, RequestExecutionValidator>();
            services.AddSingleton<IRequestHandlerManager, RequestHandlerManager>();
            services.AddSingleton<IRequestProcessor, RequestProcessor>();
            services.AddSingleton<IRouteConfigurator, RouteConfigurator>();
            services.AddSingleton<IRouteProvider, RouteProvider>();
            services.AddSingleton<ISchemaValidator, SchemaValidator>();
            services.AddSingleton<IUpstreamBuilder, UpstreamBuilder>();
            services.AddSingleton<IValueProvider, ValueProvider>();
            services.AddSingleton<DownstreamHandler>();
            services.AddSingleton<ReturnValueHandler>();
            
            return services;
        }
        
        private static IServiceCollection AddExtensions(this IServiceCollection services, IOptionsProvider optionsProvider)
        {
            var configuration = optionsProvider.Get<NtradaOptions>();
            var extensionProvider = new ExtensionProvider(configuration);
            services.AddSingleton<IExtensionProvider>(extensionProvider);
            
            foreach (var extension in extensionProvider.GetAll())
            {
                if (extension.Options.Enabled == false)
                {
                    continue;
                }
                
                extension.Extension.Add(services, optionsProvider);
            }

            return services;
        }

        public static IApplicationBuilder UseNtrada(this IApplicationBuilder app)
        {
            var newLine = Environment.NewLine;
            var logger = app.ApplicationServices.GetRequiredService<ILogger<Ntrada>>();
            logger.LogInformation($"{newLine}{newLine}{Logo}{newLine}{newLine}");
            var configuration = app.ApplicationServices.GetRequiredService<NtradaOptions>();
            if (configuration.Auth?.Enabled == true)
            {
                logger.LogInformation($"Authentication is enabled.");
                app.UseAuthentication();
            }
            else
            {
                logger.LogInformation($"Authentication is disabled.");
            }

            if (configuration.UseForwardedHeaders)
            {
                logger.LogInformation("Enabled headers forwarding.");
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.All
                });
            }
            
            app.UseExtensions();
            app.RegisterRequestHandlers();
            app.AddRoutes();

            return app;
        }

        private static void RegisterRequestHandlers(this IApplicationBuilder app)
        {
            var logger = app.ApplicationServices.GetRequiredService<ILogger<Ntrada>>();
            var configuration = app.ApplicationServices.GetRequiredService<NtradaOptions>();
            var requestHandlerManager = app.ApplicationServices.GetRequiredService<IRequestHandlerManager>();
            requestHandlerManager.AddHandler("downstream",
                app.ApplicationServices.GetRequiredService<DownstreamHandler>());
            requestHandlerManager.AddHandler("return_value",
                app.ApplicationServices.GetRequiredService<ReturnValueHandler>());

            var handlers = configuration.Modules
                .Select(m => m.Value)
                .SelectMany(m => m.Routes)
                .Select(r => r.Use)
                .Distinct()
                .ToArray();

            foreach (var handler in handlers)
            {
                if (requestHandlerManager.Get(handler) is null)
                {
                    throw new Exception($"Handler: '{handler}' was not defined.");
                }
                
                logger.LogInformation($"Added handler: ''{handler}''");
            }
        }

        private static void AddRoutes(this IApplicationBuilder app)
        {
            var configuration = app.ApplicationServices.GetRequiredService<NtradaOptions>();
            foreach (var route in configuration.Modules.SelectMany(m => m.Value.Routes))
            {
                route.Method =
                    (string.IsNullOrWhiteSpace(route.Method) ? "get" : route.Method).ToLowerInvariant();
                route.DownstreamMethod =
                    (string.IsNullOrWhiteSpace(route.DownstreamMethod) ? route.Method : route.DownstreamMethod)
                    .ToLowerInvariant();
            }

            var routeProvider = app.ApplicationServices.GetRequiredService<IRouteProvider>();
            app.UseRouter(routeProvider.Build());
        }
        
        private static void UseExtensions(this IApplicationBuilder app)
        {
            var logger = app.ApplicationServices.GetRequiredService<ILogger<Ntrada>>();
            var optionsProvider = app.ApplicationServices.GetRequiredService<IOptionsProvider>();
            var extensionProvider = app.ApplicationServices.GetRequiredService<IExtensionProvider>();
            foreach (var extension in extensionProvider.GetAll())
            {
                if (extension.Options.Enabled == false)
                {
                    continue;
                }
                
                extension.Extension.Use(app, optionsProvider);
                logger.LogInformation($"Enabled extension: '{extension.Extension.Name}' " +
                                      $"({extension.Extension.Description}) [order: {extension.Options.Order}]");
            }
        }

        private class Ntrada
        {
        }

        public static IApplicationBuilder UseRequestHandler<T>(this IApplicationBuilder app, string name)
            where T : IHandler
        {
            var requestHandlerManager = app.ApplicationServices.GetRequiredService<IRequestHandlerManager>();
            var handler = app.ApplicationServices.GetRequiredService<T>();
            requestHandlerManager.AddHandler(name, handler);

            return app;
        }

        public static bool IsNotEmpty<T>(this IEnumerable<T> enumerable) => !enumerable.IsEmpty();

        public static bool IsEmpty<T>(this IEnumerable<T> enumerable) => enumerable is null || !enumerable.Any();
    }
}