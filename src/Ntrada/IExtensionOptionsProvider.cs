using Microsoft.Extensions.Options;

namespace Ntrada
{
    internal interface IExtensionOptionsProvider
    {
        IOptions<T> Get<T>(string name) where T : class, IExtensionOptions, new();
    }
}