using System.Collections.Generic;
using Ntrada.Configuration;

namespace Ntrada.Routing
{
    public class RouteConfig
    {
        public Route Route { get; set; }
        public string Downstream { get; set; }
        public IDictionary<string, string> Claims { get; set; }
    }
}