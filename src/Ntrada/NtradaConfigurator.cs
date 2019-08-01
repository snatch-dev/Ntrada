using System;
using Microsoft.Extensions.DependencyInjection;

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
    }
}