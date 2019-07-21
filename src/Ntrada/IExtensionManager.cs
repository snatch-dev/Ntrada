using System;
using System.Collections.Generic;

namespace Ntrada
{
    public interface IExtensionManager
    {
        IDictionary<string, IExtension> Extensions { get; }
        T Get<T>(string name) where T : class, IExtension;
        void Initialize(IEnumerable<Type> registeredExtensions = null);
    }
}