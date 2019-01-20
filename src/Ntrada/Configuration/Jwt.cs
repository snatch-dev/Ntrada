using System.Collections.Generic;
using System.Linq;

namespace Ntrada.Configuration
{
    public class Jwt
    {
        public string Key { get; set; }
        public string Issuer { get; set; }
        public bool ValidateIssuer { get; set; }
        public IEnumerable<string> Issuers { get; set; } = Enumerable.Empty<string>();
        public string Audience { get; set; }
        public IEnumerable<string> Audiences { get; set; } = Enumerable.Empty<string>();
        public bool ValidateAudience { get; set; }
        public bool ValidateLifetime { get; set; }
    }
}