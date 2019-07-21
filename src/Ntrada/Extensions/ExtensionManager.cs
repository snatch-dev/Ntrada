using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace Ntrada.Extensions
{
    public class ExtensionManager : IExtensionManager
    {
        private static readonly string[] DefaultExtensions = {"downstream", "return_value"};
        private static readonly string[] AvailableExtensions = {"dispatcher"};
        private static readonly string ExtensionsDirectory = "extensions";
        private IDictionary<string, IExtension> _extensions = new Dictionary<string, IExtension>();
        private readonly NtradaConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        public ExtensionManager(NtradaConfiguration configuration, IServiceProvider serviceProvider,
            ILogger<ExtensionManager> logger)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public IDictionary<string, IExtension> Extensions => _extensions;

        public T Get<T>(string name) where T : class, IExtension
            => _extensions.SingleOrDefault(e => e.Value.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                .Value as T;

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
                Activator.CreateInstance(type, _serviceProvider) as IExtension));

            var emptyExtensionsNames = _extensions.Values.Where(e => string.IsNullOrWhiteSpace(e.Name)).ToList();
            if (emptyExtensionsNames.Any())
            {
                throw new InvalidOperationException("Extension names cannot be empty: " +
                                                    $"{string.Join(", ", emptyExtensionsNames.Select(e => e.GetType().Name))}");
            }

            var notUniqueNames = _extensions.Values.Select(e => e.Name.ToLowerInvariant())
                .GroupBy(n => n)
                .Where(n => n.Count() > 1)
                .Select(n => n.Key)
                .ToList();

            if (notUniqueNames.Any())
            {
                throw new InvalidOperationException("Extension names must be unique: " +
                                                    $"{string.Join(", ", notUniqueNames)}");
            }

            _extensions = Load(activatedExtensions.ToDictionary(e => e.Name, e => e));
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

        private IDictionary<string, IExtension> Load(IDictionary<string, IExtension> providedExtensions)
        {
            var usedExtensions = _configuration.Modules
                .SelectMany(m => m.Routes)
                .Select(r => r.Use)
                .Distinct()
                .Except(DefaultExtensions)
                .ToArray();

            var unavailableExtensions = usedExtensions.Except(AvailableExtensions).ToArray();
            if (unavailableExtensions.Any())
            {
                throw new Exception($"Unavailable extensions: '{string.Join(", ", unavailableExtensions)}'");
            }

            var enabledExtensions = _configuration.Extensions.Select(e => e.Key);
            var undefinedExtensions = usedExtensions.Except(enabledExtensions).ToArray();
            if (undefinedExtensions.Any())
            {
                throw new Exception($"Undefined extensions: '{string.Join(", ", undefinedExtensions)}'");
            }

            var extensions = new Dictionary<string, IExtension>();

            if (!_configuration.Extensions.Any())
            {
                return extensions;
            }

            var extensionsTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.GetInterfaces().Contains(typeof(IExtension)))
                .ToList();

            if (!extensionsTypes.Any())
            {
                return extensions;
            }

            foreach (var extension in _configuration.Extensions)
            {
                var extensionName = extension.Value.Use;
                if (!providedExtensions.ContainsKey(extensionName))
                {
                    throw new ArgumentException($"Extension: '{extensionName}' was not found.", nameof(extensionName));
                }

                extensions[extension.Key] = providedExtensions[extensionName];
            }

            return extensions;
        }
    }
}