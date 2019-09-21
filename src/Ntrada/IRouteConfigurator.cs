using Ntrada.Configuration;

namespace Ntrada
{
    internal interface IRouteConfigurator
    {
        RouteConfig Configure(Module module, Route route);
    }
}