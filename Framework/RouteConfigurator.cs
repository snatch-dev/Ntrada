namespace NGate.Framework
{
    public class RouteConfigurator : IRouteConfigurator
    {
        private readonly Configuration _configuration;

        public RouteConfigurator(Configuration configuration)
        {
            _configuration = configuration;
        }

        public RouteConfig Configure(Route route)
            => new RouteConfig
            {
                Route = route,
                Downstream = GetDownstream(route)
            };

        private string GetDownstream(Route route)
        {
            if (string.IsNullOrWhiteSpace(route.Downstream))
            {
                return null;
            }

            var basePath = route.Downstream.Contains("/")
                ? route.Downstream.Split('/')[0]
                : route.Downstream;

            var servicePath = _configuration.Services.TryGetValue(basePath, out var service)
                ? route.Downstream.Replace(basePath, service.Url)
                : route.Downstream;

            return servicePath.StartsWith("http") ? servicePath : $"http://{servicePath}";
        }
    }
}