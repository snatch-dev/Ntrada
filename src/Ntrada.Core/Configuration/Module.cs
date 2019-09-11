using System.Collections.Generic;
using System.Linq;

namespace Ntrada.Core.Configuration
{
    public class Module
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool? Enabled { get; set; }
        public IEnumerable<Route> Routes { get; set; }
        public IDictionary<string, Service> Services { get; set; }
    }
}