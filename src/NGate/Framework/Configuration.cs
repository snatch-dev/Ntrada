using System.Collections.Generic;

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
        public IEnumerable<Module> Modules { get; set; }
        public IDictionary<string, Extension> Extensions { get; set; }
        public Retry Retry { get; set; }
    }

    public class Retry
    {
        public int Retries { get; set; }
        public bool Exponential { get; set; }
        public int Interval { get; set; }
    }

    public class Module
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool? Enabled { get; set; }
        public IEnumerable<Route> Routes { get; set; }
        public IDictionary<string, Service> Services { get; set; }
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
        public IDictionary<string, string> Claims { get; set; }
        public IDictionary<string, IDictionary<string, string>> Policies { get; set; }
    }

    public class Jwt
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
        public IDictionary<string, string> Claims { get; set; }
        public IEnumerable<string> Policies { get; set; }
        public IEnumerable<string> Bind { get; set; }
        public IEnumerable<string> Transform { get; set; }
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