using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Ntrada.Core;

namespace Ntrada.Extensions.Jwt
{
    internal class JwtExtension : IExtension
    {
        public string Name => "jwt";
        public string Description => "JSON Web Token authentication";

        public void Add(IServiceCollection services, IOptionsProvider optionsProvider)
        {
            var options = optionsProvider.GetForExtension<JwtOptions>(Name);
            services.AddAuthorization();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(cfg =>
                {
                    cfg.TokenValidationParameters = new TokenValidationParameters
                    {
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
                        ValidIssuer = options.Issuer,
                        ValidIssuers = options.Issuers,
                        ValidAudience = options.Audience,
                        ValidAudiences = options.Audiences,
                        ValidateIssuer = options.ValidateIssuer,
                        ValidateAudience = options.ValidateAudience,
                        ValidateLifetime = options.ValidateLifetime
                    };
                });
        }

        public void Use(IApplicationBuilder app, IOptionsProvider optionsProvider)
        {
        }
    }
}
