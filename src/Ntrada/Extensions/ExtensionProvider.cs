using System;
using System.Collections.Generic;
using System.Linq;
using Ntrada.Options;

namespace Ntrada.Extensions
{
    internal sealed class ExtensionProvider : IExtensionProvider
    {
        private ISet<IEnabledExtension> _extensions = new HashSet<IEnabledExtension>();

        private readonly NtradaOptions _options;

        public ExtensionProvider(NtradaOptions options)
        {
            _options = options;
        }

        public IEnumerable<IEnabledExtension> GetAll()
        {
            if (_extensions.Any())
            {
                return _extensions;
            }

            var type = typeof(IExtension);
            var extensionTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p) && !p.IsInterface);
            var extensions = new HashSet<IEnabledExtension>();
            foreach (var extensionType in extensionTypes)
            {
                var extension = (IExtension) Activator.CreateInstance(extensionType);
                var options = _options.Extensions?.SingleOrDefault(o =>
                    o.Key.Equals(extension.Name, StringComparison.InvariantCultureIgnoreCase)).Value;

                if (options is null)
                {
                    continue;
                }

                extensions.Add(new EnabledExtension(extension, options));
            }

            _extensions = new HashSet<IEnabledExtension>(extensions.OrderBy(e => e.Options.Order));

            return _extensions;
        }
    }
}