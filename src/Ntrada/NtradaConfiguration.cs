using System.Collections.Generic;
using System.Linq;
using Ntrada.Core;
using Ntrada.Core.Configuration;

namespace Ntrada
{
    public class NtradaConfiguration : IOptions
    {
        public string Name { get; set; }
        public bool UseErrorHandler { get; set; }
        public bool UseJaeger { get; set; }
        public bool UseForwardedHeaders { get; set; }
        public bool? ForwardRequestHeaders { get; set; }
        public bool? ForwardResponseHeaders { get; set; }
        public IDictionary<string, string> RequestHeaders { get; set; }
        public IDictionary<string, string> ResponseHeaders { get; set; }
        public bool? ForwardStatusCode { get; set; }
        public bool? PassQueryString { get; set; }
        public Core.Configuration.Auth Auth { get; set; }
        public string ModulesPath { get; set; }
        public string SettingsPath { get; set; }
        public string PayloadsFolder { get; set; }
        public Cors Cors { get; set; }
        public ResourceId ResourceId { get; set; }
        public bool? GenerateRequestId { get; set; }
        public bool? GenerateTraceId { get; set; }
        public IDictionary<string, Module> Modules { get; set; }
        public Http Http { get; set; }
        public LoadBalancer LoadBalancer { get; set; }
        public bool UseLocalUrl { get; set; }
        public IDictionary<string, ExtensionOptions> Extensions { get; set; }
    }
}