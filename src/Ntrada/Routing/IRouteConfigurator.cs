using Ntrada.Configuration;

namespace Ntrada.Routing
{
    public interface IRouteConfigurator
    {
        RouteConfig Configure(Module module, Route route);
    }
}