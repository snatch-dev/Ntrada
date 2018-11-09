using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using NGate.Framework;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NGate
{
    public class Program
    {
        public static async Task Main(string[] args)
            => await CreateWebHostBuilder(args).Build().RunAsync();

        private static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            var text = File.ReadAllText("config.yml");
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .WithNamingConvention(new UnderscoredNamingConvention())
                .Build();
            var configuration = deserializer.Deserialize<Configuration>(text);
            var useJwt = configuration.Config.Authentication?.Type?.ToLowerInvariant() == "jwt";

            return WebHost.CreateDefaultBuilder(args)
                .ConfigureServices(s =>
                {
                    s.AddMvcCore()
                        .AddJsonFormatters()
                        .AddJsonOptions(o => o.SerializerSettings.Formatting = Formatting.Indented);
                    s.AddHttpClient();
                    if (!useJwt)
                    {
                        return;
                    }

                    s.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(cfg =>
                        {
                            cfg.TokenValidationParameters = new TokenValidationParameters
                            {
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding
                                    .UTF8.GetBytes(configuration.Config.Authentication.Key)),
                                ValidIssuer = configuration.Config.Authentication.Issuer,
                                ValidAudience = null,
                                ValidateAudience = false,
                                ValidateLifetime = true
                            };
                        });
                })
                .Configure(c =>
                {
                    if (useJwt)
                    {
                        c.UseAuthentication();
                    }

                    var routeProvider = new RouteProvider(c.ApplicationServices,
                        new RequestProcessor(configuration, new ValueProvider()), configuration);
                    c.UseRouter(routeProvider.Build());
                });
        }
    }
}
