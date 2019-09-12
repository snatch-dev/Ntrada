using System.Collections.Generic;
using Ntrada.Core;

namespace Ntrada.Extensions.Jwt
{
    internal class JwtOptions : IOptions
    {
        public string Key { get; set; }
        public string Issuer { get; set; }
        public bool ValidateIssuer { get; set; }
        public IEnumerable<string> Issuers { get; set; }
        public string Audience { get; set; }
        public IEnumerable<string> Audiences { get; set; }
        public bool ValidateAudience { get; set; }
        public bool ValidateLifetime { get; set; }
    }
}