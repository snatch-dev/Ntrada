using Microsoft.AspNetCore.Hosting;
using Ntrada.Extensions;

namespace Ntrada.Builders
{
    public interface INtradaBuilder : IWebHostBuilder
    {
        INtradaBuilder UseExtension<T>() where T : IExtension;
    }
}