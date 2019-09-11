using System;
using System.Collections.Generic;
using System.Linq;
using Ntrada.Core;

namespace Ntrada
{
    internal class ExtensionProvider : IExtensionProvider
    {
        private ISet<IEnabledExtension> _extensions = new HashSet<IEnabledExtension>();

        private readonly NtradaConfiguration _configuration;

        public ExtensionProvider(NtradaConfiguration configuration)
        {
            _configuration = configuration;
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
                var extension = Activator.CreateInstance(extensionType) as IExtension;
                if (extension is null)
                {
                    continue;
                }

                var options = _configuration.Extensions.SingleOrDefault(o =>
                                  o.Key.Equals(extension.Name, StringComparison.InvariantCultureIgnoreCase)).Value ??
                              new ExtensionOptions();

                extensions.Add(new EnabledExtension(extension, options));
            }

            _extensions = new HashSet<IEnabledExtension>(extensions.OrderBy(e => e.Options.Order));

            return _extensions;
        }
    }
}