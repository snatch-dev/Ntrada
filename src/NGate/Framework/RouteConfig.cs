using System.Collections.Generic;

namespace NGate.Framework
{
    public class RouteConfig
    {
        public Route Route { get; set; }
        public string Downstream { get; set; }
        public IDictionary<string, string> Claims { get; set; }
    }
}