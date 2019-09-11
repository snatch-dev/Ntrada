using Ntrada.Core.Configuration;

namespace Ntrada
{
    public interface IUpstreamBuilder
    {
        string Build(Module module, Route route);
    }
}