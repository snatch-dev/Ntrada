using System.Collections.Generic;
using System.Linq;

namespace Ntrada.Configuration
{
    public class NtradaConfiguration
    {
        public bool UseErrorHandler { get; set; }
        public bool UseForwardedHeaders { get; set; }
        public IDictionary<string, string> RequestHeaders { get; set; }
        public IDictionary<string, string> ResponseHeaders { get; set; }
        public bool? ForwardStatusCode { get; set; }
        public bool? PassQueryString { get; set; }
        public Auth Auth { get; set; }
        public string ModulesPath { get; set; }
        public string SettingsPath { get; set; }
        public string PayloadsFolder { get; set; }
        public Cors Cors { get; set; }
        public ResourceId ResourceId { get; set; }
        public IEnumerable<Module> Modules { get; set; } = Enumerable.Empty<Module>();
        public IDictionary<string, Extension> Extensions { get; set; } = new Dictionary<string, Extension>();
        public Http Http { get; set; }
    }
}