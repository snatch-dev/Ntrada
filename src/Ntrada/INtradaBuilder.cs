using Microsoft.AspNetCore.Hosting;

namespace Ntrada
{
    public interface INtradaBuilder : IWebHostBuilder
    {
        INtradaBuilder UseExtension<T>() where T : IExtension;
    }
}