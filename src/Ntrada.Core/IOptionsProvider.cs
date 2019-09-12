namespace Ntrada.Core
{
    public interface IOptionsProvider
    {
        T Get<T>(string name = null) where T : class, IOptions, new();
        T GetForExtension<T>(string name) where T : class, IOptions, new();
    }
}