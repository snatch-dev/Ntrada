using System;
using Jaeger.Util;
using Microsoft.Extensions.DependencyInjection;

namespace Ntrada
{
    public interface INtradaConfigurator
    {
        INtradaConfigurator ConfigureHttpClient(Action<IHttpClientBuilder> builder);
        Action<IHttpClient, ExecutionData> BeforeHttpRequest { get; set; }
    }
}