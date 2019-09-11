using System.Collections.Generic;
using System.Linq;

namespace Ntrada.Core.Configuration
{
    public class Route
    {
        public ResourceId ResourceId { get; set; }
        public string Upstream { get; set; }
        public string Method { get; set; }
        public string Use { get; set; }
        public string Downstream { get; set; }
        public string DownstreamMethod { get; set; }
        public bool? PassQueryString { get; set; }
        public string ReturnValue { get; set; }
        public string Payload { get; set; }
        public string Schema { get; set; }
        public bool? Auth { get; set; }
        public IDictionary<string, string> RequestHeaders { get; set; }
        public IDictionary<string, string> ResponseHeaders { get; set; }
        public bool? ForwardRequestHeaders { get; set; }
        public bool? ForwardResponseHeaders { get; set; }
        public bool? ForwardStatusCode { get; set; }
        public bool? GenerateRequestId { get; set; }
        public bool? GenerateTraceId { get; set; }
        public OnError OnError { get; set; }
        public OnSuccess OnSuccess { get; set; }
        public IDictionary<string, string> Claims { get; set; }
        public IEnumerable<string> Policies { get; set; }
        public IEnumerable<string> Bind { get; set; }
        public IEnumerable<string> Transform { get; set; }
        public IDictionary<string, string> Config { get; set; }
    }
}