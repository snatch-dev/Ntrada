using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Ntrada.Core
{
    public static class Extensions
    {
        public static IApplicationBuilder UseRequestHandler<T>(this IApplicationBuilder app, string name)
            where T : IHandler
        {
            var requestHandlerManager = app.ApplicationServices.GetRequiredService<IRequestHandlerManager>();
            var handler = app.ApplicationServices.GetRequiredService<T>();
            requestHandlerManager.AddHandler(name, handler);

            return app;
        }

        public static bool IsNotEmpty<T>(this IEnumerable<T> enumerable) => !enumerable.IsEmpty();

        public static bool IsEmpty<T>(this IEnumerable<T> enumerable) => enumerable is null || !enumerable.Any();
    }
}