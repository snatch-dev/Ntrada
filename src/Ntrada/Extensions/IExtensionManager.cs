using System.Collections.Generic;

namespace Ntrada.Extensions
{
    public interface IExtensionManager
    {
        IEnumerable<IExtension> GetAll();
        T Get<T>(string name) where T : class, IExtension;
        void Initialize();
    }
}