using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ntrada
{
    public class NtradaBuilder : INtradaBuilder
    {
        private readonly ISet<Type> _extensions = new HashSet<Type>();
        private readonly IWebHostBuilder _webHostBuilder;

        public NtradaBuilder(IWebHostBuilder webHostBuilder)
        {
            _webHostBuilder = webHostBuilder;
        }

        internal IEnumerable<Type> Extensions => _extensions;

        public IWebHost Build() => _webHostBuilder.Build();

        public IWebHostBuilder ConfigureAppConfiguration(
            Action<WebHostBuilderContext, IConfigurationBuilder> configureDelegate)
            => _webHostBuilder.ConfigureAppConfiguration(configureDelegate);

        public IWebHostBuilder ConfigureServices(Action<IServiceCollection> configureServices)
            => _webHostBuilder.ConfigureServices(configureServices);

        public IWebHostBuilder ConfigureServices(Action<WebHostBuilderContext, IServiceCollection> configureServices)
            => _webHostBuilder.ConfigureServices(configureServices);

        public string GetSetting(string key)
            => _webHostBuilder.GetSetting(key);

        public IWebHostBuilder UseSetting(string key, string value)
            => _webHostBuilder.UseSetting(key, value);

        public INtradaBuilder UseExtension<T>() where T : IExtension
        {
            _extensions.Add(typeof(T));

            return this;
        }
    }
}