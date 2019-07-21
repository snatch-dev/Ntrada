using Ntrada.Configuration;

namespace Ntrada
{
    public interface IRouteConfigurator
    {
        RouteConfig Configure(Module module, Route route);
    }
}