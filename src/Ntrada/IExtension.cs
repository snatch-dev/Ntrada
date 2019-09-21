using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Ntrada
{
    public interface IExtension
    {
        string Name { get; }
        string Description { get; }
        void Add(IServiceCollection services, IOptionsProvider optionsProvider);
        void Use(IApplicationBuilder app, IOptionsProvider optionsProvider);
    }
}