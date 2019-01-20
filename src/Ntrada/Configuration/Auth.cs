using System.Collections.Generic;

namespace Ntrada.Configuration
{
    public class Auth
    {
        public string Type { get; set; }
        public bool Global { get; set; }
        public Jwt Jwt { get; set; }
        public IDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>();
        public IDictionary<string, Policy> Policies { get; set; } = new Dictionary<string, Policy>();
    }
}