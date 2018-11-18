using System.Dynamic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace NGate.Framework
{
    public class ExecutionData
    {
        public string RequestId { get; set; }
        public Route Route { get; set; }
        public HttpRequest Request { get; set; }
        public HttpResponse Response { get; set; }
        public RouteData Data { get; set; }
        public string Downstream { get; set; }
        public ExpandoObject Payload { get; set; }
        public string ContentType { get; set; }
        public string ResourceId { get; set; }
        public string UserId { get; set; }
    }
}