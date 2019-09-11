using System.Collections.Generic;

namespace Ntrada.Core.Configuration
{
    public class Auth
    {
        public bool Enabled { get; set; }
        public string Type { get; set; }
        public bool Global { get; set; }
        public IDictionary<string, string> Claims { get; set; }
        public IDictionary<string, Policy> Policies { get; set; }
    }
}