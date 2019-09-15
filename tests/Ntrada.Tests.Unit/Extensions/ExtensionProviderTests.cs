using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Ntrada.Core;
using Ntrada.Extensions;
using Ntrada.Options;
using Shouldly;
using Xunit;

namespace Ntrada.Tests.Unit.Extensions
{
    [ExcludeFromCodeCoverage]
    public class ExtensionProviderTests
    {
        private IEnumerable<IEnabledExtension> Act() => _extensionProvider.GetAll();

        [Fact]
        public void get_all_should_return_empty_collection_if_extensions_are_not_defined()
        {
            var result = Act();
            result.ShouldBeEmpty();
        }
        
        [Fact]
        public void get_all_should_return_not_empty_collection_if_at_least_one_extension_was_defined()
        {
            _options.Extensions = new Dictionary<string, ExtensionOptions>
            {
                [ExtensionName] = new ExtensionOptions(),
            };
            
            var result = Act();
            result.ShouldNotBeEmpty();
        }
        
        [Fact]
        public void get_all_should_return_extension_with_not_null_extension_property()
        {
            var options = new ExtensionOptions
            {
                Enabled = true,
                Order = 1
            };
            _options.Extensions = new Dictionary<string, ExtensionOptions>
            {
                [ExtensionName] = options,
            };
            
            var result = Act();
            var extension = result.SingleOrDefault();
            extension.ShouldNotBeNull();
            extension.Extension.ShouldNotBeNull();
        }
        
        [Fact]
        public void get_all_should_return_extension_with_the_same_options_property()
        {
            var options = new ExtensionOptions
            {
                Enabled = true,
                Order = 1
            };
            _options.Extensions = new Dictionary<string, ExtensionOptions>
            {
                [ExtensionName] = options,
            };
            
            var result = Act();
            var extension = result.SingleOrDefault();
            extension.ShouldNotBeNull();
            extension.Options.Enabled.ShouldBe(options.Enabled);
            extension.Options.Order.ShouldBe(options.Order);
        }
        
        [Fact]
        public void get_all_should_return_the_same_extensions_if_invoked_twice()
        {
            _options.Extensions = new Dictionary<string, ExtensionOptions>
            {
                [ExtensionName] = new ExtensionOptions(),
            };

            var result = Act();
            var result2 = Act();
            result.ShouldBeSameAs(result2);
        }

        #region Arrange

        private const string ExtensionName = "test";
        private readonly IExtensionProvider _extensionProvider;
        private readonly NtradaOptions _options;

        public ExtensionProviderTests()
        {
            _options = new NtradaOptions();
            _extensionProvider =  new ExtensionProvider(_options);
        }

        private class TestExtension : IExtension
        {
            public string Name { get; } = ExtensionName;
            public string Description { get; } = ExtensionName;
            
            public void Add(IServiceCollection services, IOptionsProvider optionsProvider)
            {
            }

            public void Use(IApplicationBuilder app, IOptionsProvider optionsProvider)
            {
            }
        }

        #endregion
    }
}