using System.Collections.Generic;
using System.Linq;

namespace Ntrada.Configuration
{
    public class Module
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool? Enabled { get; set; }
        public IEnumerable<Route> Routes { get; set; } = Enumerable.Empty<Route>();
        public IDictionary<string, Service> Services { get; set; } = new Dictionary<string, Service>();
    }
}