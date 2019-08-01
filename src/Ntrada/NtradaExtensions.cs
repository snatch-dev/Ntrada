using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Ntrada.Auth;
using Ntrada.Configuration;
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


        public static IServiceCollection AddNtrada(this IServiceCollection services, string configPath = "ntrada.yml",
            Action<INtradaConfigurator> ntrada = null)
        {

            var configPathVariable = Environment.GetEnvironmentVariable("NTRADA_CONFIG");
            if (!string.IsNullOrWhiteSpace(configPathVariable))
            {
                configPath = configPathVariable;
            }

            if (string.IsNullOrWhiteSpace(configPath))
            {
                configPath = "ntrada.yml";
            }

            if (!configPath.EndsWith(".yml"))
            {
                configPath = $"{configPath}.yml";
            }

            if (!File.Exists(configPath))
            {
                throw new ArgumentException($"Ntrada config was not found under: '{configPath}'", nameof(configPath));
            }

            var text = File.ReadAllText(configPath);
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .WithNamingConvention(new UnderscoredNamingConvention())
                .Build();
            var configuration = deserializer.Deserialize<NtradaConfiguration>(text);
            var authenticationConfig = configuration.Auth;
            var useJwt = authenticationConfig?.Type?.ToLowerInvariant() == "jwt";
            var useForwardedHeaders = configuration.UseForwardedHeaders;
            var cors = configuration?.Cors;
            var useCors = cors?.Enabled == true;
            var useErrorHandler = configuration.UseErrorHandler;
            var useJaeger = configuration.UseJaeger;
            var http = configuration.Http ?? new Http();
            if (configuration.SettingsPath is null)
            {
                configuration.SettingsPath = "Settings";
            }

            if (configuration.SettingsPath.EndsWith("/"))
            {
                configuration.SettingsPath = configuration.SettingsPath
                    .Substring(0, configuration.SettingsPath.Length - 1);
            }

            if (configuration.PayloadsFolder is null)
            {
                configuration.PayloadsFolder = "Payloads";
            }

            if (configuration.PayloadsFolder.EndsWith("/"))
            {
                configuration.PayloadsFolder = configuration.PayloadsFolder
                    .Substring(0, configuration.PayloadsFolder.Length - 1);
            }

            var modules = new HashSet<Module>();
            var modulesPath = string.IsNullOrWhiteSpace(configuration.ModulesPath)
                ? "Modules"
                : configuration.ModulesPath;
            if (modulesPath.EndsWith("/"))
            {
                modulesPath = modulesPath.Substring(0, modulesPath.Length - 1);
            }

            if (Directory.Exists(modulesPath))
            {
                var modulesPaths = Directory.EnumerateDirectories(modulesPath).ToList();
                foreach (var modulePath in modulesPaths)
                {
                    var fullModulePath = $"{modulePath}/module.yml";
                    if (!File.Exists(fullModulePath))
                    {
                        continue;
                    }

                    var module = deserializer.Deserialize<Module>(File.ReadAllText(fullModulePath));
                    modules.Add(module);
                }

                var allModules = new List<Module>();
                allModules.AddRange(configuration.Modules);
                allModules.AddRange(modules);
                configuration.Modules = allModules;
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
            
            if (!(authenticationConfig is null) && useJwt)
            {
                var jwtConfig = authenticationConfig.Jwt;
                services.AddAuthorization();
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(cfg =>
                    {
                        cfg.TokenValidationParameters = new TokenValidationParameters
                        {
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding
                                .UTF8.GetBytes(jwtConfig.Key)),
                            ValidIssuer = jwtConfig.Issuer,
                            ValidIssuers = jwtConfig.Issuers,
                            ValidAudience = jwtConfig.Audience,
                            ValidAudiences = jwtConfig.Audiences,
                            ValidateIssuer = jwtConfig.ValidateIssuer,
                            ValidateAudience = jwtConfig.ValidateAudience,
                            ValidateLifetime = jwtConfig.ValidateLifetime
                        };
                    });
            }

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

            return services;
        }


        public static IApplicationBuilder UseNtrada(this IApplicationBuilder app)
        {
            var newLine = Environment.NewLine;
            Console.WriteLine($"{newLine}{newLine}{Logo}{newLine}{newLine}");
            var configuration = app.ApplicationServices.GetRequiredService<NtradaConfiguration>();
            var authenticationConfig = configuration.Auth;
            var useJwt = authenticationConfig?.Type?.ToLowerInvariant() == "jwt";
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

            if (useJwt)
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

            foreach (var route in configuration.Modules.SelectMany(m => m.Routes))
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

        public static IApplicationBuilder UseRequestHandler<T>(this IApplicationBuilder app, string name)
            where T : IHandler
        {
            var requestHandlerManager = app.ApplicationServices.GetRequiredService<IRequestHandlerManager>();
            var handler = app.ApplicationServices.GetRequiredService<T>();
            requestHandlerManager.AddHandler(name, handler);

            return app;
        }
    }
}