using System.Collections.Generic;
using System.Linq;

namespace NGate.Framework
{
    public class Configuration
    {
        public bool UseErrorHandler { get; set; }
        public bool UseForwardedHeaders { get; set; }
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

    public class Http
    {
        public int Retries { get; set; }
        public bool Exponential { get; set; }
        public double Interval { get; set; }
    }

    public class Module
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool? Enabled { get; set; }
        public IEnumerable<Route> Routes { get; set; } = Enumerable.Empty<Route>();
        public IDictionary<string, Service> Services { get; set; } = new Dictionary<string, Service>();
    }

    public class ResourceId
    {
        public bool Generate { get; set; }
        public string Property { get; set; }
    }

    public class Cors
    {
        public bool Enabled { get; set; }
        public IEnumerable<string> Headers { get; set; }
    }

    public class Auth
    {
        public string Type { get; set; }
        public bool Global { get; set; }
        public Jwt Jwt { get; set; }
        public IDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>();
        public IDictionary<string, Policy> Policies { get; set; } = new Dictionary<string, Policy>();
    }

    public class Policy
    {
        public IDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>(); 
    }

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

    public class Route
    {
        public bool? GenerateResourceId { get; set; }
        public string Upstream { get; set; }
        public string Method { get; set; }
        public string Use { get; set; }
        public string Downstream { get; set; }
        public string DownstreamMethod { get; set; }
        public bool? PassQueryString { get; set; }
        public string ReturnValue { get; set; }
        public string Payload { get; set; }
        public string Schema { get; set; }
        public string Exchange { get; set; }
        public string RoutingKey { get; set; }
        public bool? Auth { get; set; }
        public OnError OnError { get; set; }
        public OnSuccess OnSuccess { get; set; }
        public IDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>();
        public IEnumerable<string> Policies { get; set; } = Enumerable.Empty<string>();
        public IEnumerable<string> Bind { get; set; } = Enumerable.Empty<string>();
        public IEnumerable<string> Transform { get; set; } = Enumerable.Empty<string>();
    }

    public class OnSuccess
    {
        public int Code { get; set; } = 200;
        public object Data { get; set; }
    }
    
    public class OnError
    {
        public int Code { get; set; } = 200;
        public object Data { get; set; }
    }

    public class Extension
    {
        public string Use { get; set; }
        public string Configuration { get; set; }
    }

    public class Service
    {
        public string Url { get; set; }
    }
}