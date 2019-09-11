using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Ntrada.Auth;
using Ntrada.Core;
using Ntrada.Core.Configuration;
using Ntrada.Handlers;
using Ntrada.Middleware;
using Ntrada.Requests;
using Ntrada.Routing;
using Ntrada.Tracing;
using Polly;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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


        public static IServiceCollection AddNtrada(this IServiceCollection services, Action<INtradaConfigurator> ntrada = null)
        {
            IConfiguration cfg = null;
            using (var scope = services.BuildServiceProvider().CreateScope())
            {
                cfg = scope.ServiceProvider.GetService<IConfiguration>();
            }
            
            var optionsProvider = new OptionsProvider(cfg);
            services.AddSingleton<IOptionsProvider>(optionsProvider);
            
            var configuration = optionsProvider.Get<NtradaConfiguration>();
            var authenticationConfig = configuration.Auth;
            var cors = configuration?.Cors;
            var useCors = cors?.Enabled == true;
            var useErrorHandler = configuration.UseErrorHandler;
            var http = configuration.Http ?? new Http();
            if (configuration.PayloadsFolder is null)
            {
                configuration.PayloadsFolder = "Payloads";
            }

            if (configuration.PayloadsFolder.EndsWith("/"))
            {
                configuration.PayloadsFolder = configuration.PayloadsFolder
                    .Substring(0, configuration.PayloadsFolder.Length - 1);
            }

            services.AddSingleton(configuration);
            services.AddMvcCore()
                .AddJsonFormatters()
                .AddJsonOptions(o => o.SerializerSettings.Formatting = Formatting.Indented);
            services.AddLogging(
                builder =>
                {
                    builder.AddFilter("Microsoft", LogLevel.Warning)
                        .AddFilter("System", LogLevel.Warning)
                        .AddConsole();
                });
            services.AddJaeger();

            var httpClientBuilder = services.AddHttpClient("ntrada");
            httpClientBuilder.AddTransientHttpErrorPolicy(p =>
                p.WaitAndRetryAsync(http.Retries, retryAttempt =>
                {
                    var interval = http.Exponential
                        ? Math.Pow(http.Interval, retryAttempt)
                        : http.Interval;

                    return TimeSpan.FromSeconds(interval);
                }));
            
            var ntradaBuilder = new NtradaConfigurator(httpClientBuilder);
            ntrada?.Invoke(ntradaBuilder);

            if (useErrorHandler)
            {
                services.AddTransient<ErrorHandlerMiddleware>();
            }

            if (useCors)
            {
                services.AddCors(options =>
                {
                    var headers = cors?.Headers ?? Enumerable.Empty<string>();
                    options.AddPolicy("CorsPolicy", builder =>
                    {
                        builder.AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .WithExposedHeaders(headers.ToArray());
                    });
                });
            }

            services.AddSingleton<IAccessValidator, AccessValidator>();
            services.AddSingleton<IDownstreamBuilder, DownstreamBuilder>();
            services.AddSingleton<IPayloadBuilder, PayloadBuilder>();
            services.AddSingleton<IPayloadManager, PayloadManager>();
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
            Console.WriteLine($"{newLine}{newLine}{Logo}{newLine}{newLine}");
            var configuration = app.ApplicationServices.GetRequiredService<NtradaConfiguration>();
            var authenticationConfig = configuration.Auth;
            var authenticationEnabled = authenticationConfig?.Enabled == true;
            var useForwardedHeaders = configuration.UseForwardedHeaders;
            var cors = configuration?.Cors;
            var useCors = cors?.Enabled == true;
            var useErrorHandler = configuration.UseErrorHandler;
            var useJaeger = configuration.UseJaeger;
            var http = configuration.Http ?? new Http();
            if (useErrorHandler)
            {
                app.UseMiddleware<ErrorHandlerMiddleware>();
            }

            if (useJaeger)
            {
                app.UseJaeger();
            }

            if (useCors)
            {
                app.UseCors("CorsPolicy");
            }

            if (authenticationEnabled)
            {
                app.UseAuthentication();
            }

            if (useForwardedHeaders)
            {
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.All
                });
            }
            
            var optionsProvider = app.ApplicationServices.GetRequiredService<IOptionsProvider>();
            var extensionProvider = app.ApplicationServices.GetRequiredService<IExtensionProvider>();
            foreach (var extension in extensionProvider.GetAll())
            {
                if (extension.Options.Enabled == false)
                {
                    continue;
                }
                
                extension.Extension.Use(app, optionsProvider);
            }

            foreach (var route in configuration.Modules.SelectMany(m => m.Value.Routes))
            {
                route.Method =
                    (string.IsNullOrWhiteSpace(route.Method) ? "get" : route.Method).ToLowerInvariant();
                route.DownstreamMethod =
                    (string.IsNullOrWhiteSpace(route.DownstreamMethod) ? route.Method : route.DownstreamMethod)
                    .ToLowerInvariant();
            }

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
                    throw new Exception($"Handler '{handler}' was not defined.");
                }
            }

            var routeProvider = app.ApplicationServices.GetRequiredService<IRouteProvider>();
            app.UseRouter(routeProvider.Build());

            return app;
        }
    }
}