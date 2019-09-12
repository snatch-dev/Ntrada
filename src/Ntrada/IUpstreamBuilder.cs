using Ntrada.Core.Configuration;

namespace Ntrada
{
    internal interface IUpstreamBuilder
    {
        string Build(Module module, Route route);
    }
}