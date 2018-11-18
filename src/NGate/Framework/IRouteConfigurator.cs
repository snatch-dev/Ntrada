namespace NGate.Framework
{
    public interface IRouteConfigurator
    {
        RouteConfig Configure(Module module, Route route);
    }
}