using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Ntrada.Configuration;

namespace Ntrada.Extensions
{
    public class ExtensionManager : IExtensionManager
    {
        private ISet<IExtension> _extensions = new HashSet<IExtension>();
        private readonly NtradaConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly string ExtensionsDirectory = "extensions";

        public ExtensionManager(NtradaConfiguration configuration, ILogger<ExtensionManager> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public IEnumerable<IExtension> GetAll() => _extensions;

        public T Get<T>(string name) where T : class, IExtension
            => _extensions.SingleOrDefault(e => e.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) as T;

        public void Initialize()
        {
            _logger.LogInformation($"Initializing extension manager.");

            var path = $"{Directory.GetCurrentDirectory()}/{ExtensionsDirectory}";
            if (!Directory.Exists(path))
            {
                _logger.LogInformation($"Extensions directory: '{ExtensionsDirectory}' was not found.");

                return;
            }

            var extensionsAssemblies = Directory.EnumerateFiles(path, "Ntrada.Extensions.*.dll")
                .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath)
                .ToArray();

            if (!extensionsAssemblies.Any())
            {
                _logger.LogInformation("No assemblies containing extensions have been found.");

                return;
            }

            _logger.LogInformation($"Found {extensionsAssemblies.Length} assemblies containing extensions.");

            var extensions = new List<IExtension>();
            foreach (var assembly in extensionsAssemblies)
            {
                var serviceType = typeof(IExtension);
                var types = assembly.ExportedTypes.Where(t => serviceType.IsAssignableFrom(t)).ToArray();
                extensions.AddRange(types.Select(type =>
                    Activator.CreateInstance(type, new[] {_configuration}) as IExtension));
            }

            var emptyExtensionsNames = _extensions.Where(e => string.IsNullOrWhiteSpace(e.Name)).ToList();
            if (emptyExtensionsNames.Any())
            {
                throw new InvalidOperationException("Extension names cannot be empty: " +
                                                    $"{string.Join(", ", emptyExtensionsNames.Select(e => e.GetType().Name))}");
            }

            var notUniqueNames = _extensions.Select(e => e.Name.ToLowerInvariant())
                .GroupBy(n => n)
                .Where(n => n.Count() > 1)
                .Select(n => n.Key)
                .ToList();

            if (notUniqueNames.Any())
            {
                throw new InvalidOperationException("Extension names must be unique: " +
                                                    $"{string.Join(", ", notUniqueNames)}");
            }

            _extensions = new HashSet<IExtension>(extensions);

            _logger.LogInformation($"Loaded {extensions.Count} extensions: " +
                                   $"{string.Join(", ", extensions.Select(e => e.Name))}");
        }
    }
}