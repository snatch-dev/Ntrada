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
        private static readonly string ExtensionsDirectory = "extensions";
        private ISet<IExtension> _extensions = new HashSet<IExtension>();
        private readonly NtradaConfiguration _configuration;
        private readonly ILogger _logger;

        public ExtensionManager(NtradaConfiguration configuration, ILogger<ExtensionManager> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public IEnumerable<IExtension> GetAll() => _extensions;

        public T Get<T>(string name) where T : class, IExtension
            => _extensions.SingleOrDefault(e => e.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) as T;

        public void Initialize(IEnumerable<Type> registeredExtensions = null)
        {
            _logger.LogInformation($"Initializing extension manager.");

            var usedExtensions = registeredExtensions is null ? Array.Empty<Type>() : registeredExtensions.ToArray();
            var externalExtensions = LoadExternalExtensions().ToArray();
            if (usedExtensions.Any())
            {
                _logger.LogInformation($"Extensions: {usedExtensions.Length} registered, " +
                                       $"{externalExtensions.Length} external.");
            }

            var allExtensions = usedExtensions.Union(externalExtensions);
            var activatedExtensions = new List<IExtension>();

            activatedExtensions.AddRange(allExtensions.Select(type =>
                Activator.CreateInstance(type, _configuration) as IExtension));

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

            _extensions = new HashSet<IExtension>(activatedExtensions);

            _logger.LogInformation($"Loaded {activatedExtensions.Count} extension(s): " +
                                   $"{string.Join(", ", activatedExtensions.Select(e => e.Name))}");
        }

        private IEnumerable<Type> LoadExternalExtensions()
        {
            var path = $"{Directory.GetCurrentDirectory()}/{ExtensionsDirectory}";
            if (!Directory.Exists(path))
            {
                _logger.LogInformation($"Extensions directory: '{ExtensionsDirectory}' was not found.");

                return Enumerable.Empty<Type>();
            }

            var extensionsAssemblies = Directory.EnumerateFiles(path, "Ntrada.Extensions.*.dll")
                .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath)
                .ToArray();

            if (!extensionsAssemblies.Any())
            {
                _logger.LogInformation("No assemblies containing extensions have been found.");

                return Enumerable.Empty<Type>();
            }

            var extensions = new List<Type>();
            _logger.LogInformation($"Found {extensionsAssemblies.Length} assemblies containing extensions.");

            foreach (var assembly in extensionsAssemblies)
            {
                var serviceType = typeof(IExtension);
                var types = assembly.ExportedTypes.Where(t => serviceType.IsAssignableFrom(t)).ToArray();
                extensions.AddRange(types);
            }

            return extensions;
        }
    }
}