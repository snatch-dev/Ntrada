using System.Collections.Generic;

namespace NGate.Framework
{
    public class Configuration
    {
        public Config Config { get; set; }
        public IDictionary<string, IEnumerable<Route>> Routes { get; set; }
        public IDictionary<string, Extension> Extensions { get; set; }
        public IDictionary<string, Service> Services { get; set; }
    }

    public class Config
    {
        public bool GenerateResourceId { get; set; }
        public bool? PassQueryString { get; set; }
        public Authentication Authentication { get; set; }
        public string PayloadsPath { get; set; }
    }

    public class Authentication
    {
        public string Type { get; set; }
        public bool Global { get; set; }
        public string Key { get; set; }
        public string Issuer { get; set; }
        public bool ValidateIssuer { get; set; }
        public IEnumerable<string> Issuers { get; set; }
        public string Audience { get; set; }
        public IEnumerable<string> Audiences { get; set; }
        public bool ValidateAudience { get; set; }
        public bool ValidateLifetime { get; set; }
        public Claims Claims { get; set; }
    }

    public class Claims
    {
        public IDictionary<string, string> Map { get; set; }
        public IDictionary<string, string> Required { get; set; }
    }

    public class RoutesGroup
    {
        public string Name { get; set; }
        public List<Route> Routes { get; set; }
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
        public string Scheme { get; set; }
        public string Exchange { get; set; }
        public string RoutingKey { get; set; }
        public bool? Auth { get; set; }
        public IDictionary<string, string> Claims { get; set; }
        public IEnumerable<string> Set { get; set; }
        public IEnumerable<string> Transform { get; set; }
    }

    public class Extension
    {
        public string Use { get; set; }
    }

    public class Service
    {
        public string Url { get; set; }
    }
}