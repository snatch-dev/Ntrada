using Ntrada.Core;
using Ntrada.Core.Configuration;

namespace Ntrada
{
    public interface IRouteConfigurator
    {
        RouteConfig Configure(Module module, Route route);
    }
}