using System;
using Jaeger.Util;
using Microsoft.Extensions.DependencyInjection;
using Ntrada.Core;

namespace Ntrada
{
    internal class NtradaConfigurator : INtradaConfigurator
    {
        private readonly IHttpClientBuilder _httpClientBuilder;

        public NtradaConfigurator(IHttpClientBuilder httpClientBuilder)
        {
            _httpClientBuilder = httpClientBuilder;
        }

        public INtradaConfigurator ConfigureHttpClient(Action<IHttpClientBuilder> builder)
        {
            builder(_httpClientBuilder);

            return this;
        }

        public Action<IHttpClient, ExecutionData> BeforeHttpRequest { get; set; }
    }
}