using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ntrada
{
    public interface INtradaConfigurator
    {
        INtradaConfigurator ConfigureHttpClient(Action<IHttpClientBuilder> builder);
    }
}