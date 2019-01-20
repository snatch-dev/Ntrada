using System.Collections.Generic;

namespace Ntrada.Configuration
{
    public class Policy
    {
        public IDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>(); 
    }
}