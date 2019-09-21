using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Route = Ntrada.Configuration.Route;

namespace Ntrada
{
    public class ExecutionData
    {
        public string RequestId { get; set; }
        public string ResourceId { get; set; }
        public string TraceId { get; set; }
        public string UserId { get; set; }
        public IDictionary<string, string> Claims { get; set; }
        public string ContentType { get; set; }
        public Route Route { get; set; }
        public HttpRequest Request { get; set; }
        public HttpResponse Response { get; set; }
        public RouteData Data { get; set; }
        public string Downstream { get; set; }
        public ExpandoObject Payload { get; set; }
        public bool HasPayload { get; set; }
        public IEnumerable<Error> ValidationErrors { get; set; } = Enumerable.Empty<Error>();
        public bool IsPayloadValid => !ValidationErrors.Any();
    }
}