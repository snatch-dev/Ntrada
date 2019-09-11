using System.Collections.Generic;

namespace Ntrada
{
    internal interface IExtensionProvider
    {
        IEnumerable<IEnabledExtension> GetAll();
    }
}