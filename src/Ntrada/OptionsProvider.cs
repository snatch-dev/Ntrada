using Microsoft.Extensions.Configuration;
using Ntrada.Core;

namespace Ntrada
{
    public class OptionsProvider : IOptionsProvider
    {
        private readonly IConfiguration _configuration;

        public OptionsProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public T Get<T>(string name = null) where T : class, IOptions, new()
            => GetOptions<T>(name);

        public T GetForExtension<T>(string name) where T : class, IOptions, new()
            => GetOptions<T>(name, "extensions");

        private T GetOptions<T>(string name, string section = null) where T : class, IOptions, new()
        {
            var sectionName = string.IsNullOrWhiteSpace(section)
                ? string.IsNullOrWhiteSpace(name) ? string.Empty : name
                : $"{section}:{name}";
            var options = new T();
            if (string.IsNullOrWhiteSpace(sectionName))
            {
                _configuration.Bind(options);
            }
            else
            {
                _configuration.GetSection(sectionName).Bind(options);
            }

            return options;
        }
    }
}