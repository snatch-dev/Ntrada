using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ntrada.Auth;
using Ntrada.Core.Configuration;
using Ntrada.Options;
using Shouldly;
using Xunit;

namespace Ntrada.Tests.Unit.Auth
{
    [ExcludeFromCodeCoverage]
    public class AuthenticationManagerTests
    {
        private Task<bool> Act() => _authenticationManager.TryAuthenticateAsync(_httpContext.Request, _routeConfig);

        [Fact]
        public async Task try_authenticate_should_return_true_if_auth_is_disabled()
        {
            var result = await Act();
            result.ShouldBeTrue();
            await _authenticationService.DidNotReceiveWithAnyArgs().AuthenticateAsync(null, null);
        }

        [Fact]
        public async Task try_authenticate_should_return_true_if_global_auth_is_disabled()
        {
            _options.Auth = new Core.Configuration.Auth
            {
                Enabled = true
            };
            var result = await Act();
            result.ShouldBeTrue();
            await _authenticationService.DidNotReceiveWithAnyArgs().AuthenticateAsync(null, null);
        }

        [Fact]
        public async Task try_authenticate_should_return_true_if_route_auth_is_disabled()
        {
            _options.Auth = new Core.Configuration.Auth
            {
                Enabled = true
            };
            _routeConfig.Route = new Route
            {
                Auth = false
            };
            var result = await Act();
            result.ShouldBeTrue();
            await _authenticationService.DidNotReceiveWithAnyArgs().AuthenticateAsync(null, null);
        }

        [Fact]
        public async Task try_authenticate_should_return_false_if_global_auth_is_enabled_and_user_is_not_authenticated()
        {
            _options.Auth = new Core.Configuration.Auth
            {
                Enabled = true,
                Global = true
            };
            SetFailedAuth();
            var result = await Act();
            result.ShouldBeFalse();
            await _authenticationService.Received().AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string>());
        }

        [Fact]
        public async Task try_authenticate_should_return_false_if_route_auth_is_enabled_and_user_is_not_authenticated()
        {
            _options.Auth = new Core.Configuration.Auth
            {
                Enabled = true,
                Global = false
            };
            _routeConfig.Route = new Route
            {
                Auth = true
            };
            SetFailedAuth();
            var result = await Act();
            result.ShouldBeFalse();
            await _authenticationService.Received().AuthenticateAsync(_httpContext, Arg.Any<string>());
        }
        
        [Fact]
        public async Task try_authenticate_should_return_true_if_global_auth_is_enabled_and_user_is_authenticated()
        {
            _options.Auth = new Core.Configuration.Auth
            {
                Enabled = true,
                Global = true
            };
            SetSuccessfulAuth();
            var result = await Act();
            result.ShouldBeTrue();
            await _authenticationService.Received().AuthenticateAsync(_httpContext, Arg.Any<string>());
        }

        [Fact]
        public async Task try_authenticate_should_return_true_if_route_auth_is_enabled_and_user_is_authenticated()
        {
            _options.Auth = new Core.Configuration.Auth
            {
                Enabled = true,
                Global = false
            };
            _routeConfig.Route = new Route
            {
                Auth = true
            };
            SetSuccessfulAuth();
            var result = await Act();
            result.ShouldBeTrue();
            await _authenticationService.Received().AuthenticateAsync(_httpContext, Arg.Any<string>());
        }

        #region  Arrange

        private readonly IAuthenticationManager _authenticationManager;
        private readonly NtradaOptions _options;
        private readonly HttpContext _httpContext;
        private readonly RouteConfig _routeConfig;
        private readonly IAuthenticationService _authenticationService;
        private const string Scheme = "test";

        public AuthenticationManagerTests()
        {
            _options = new NtradaOptions();
            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(typeof(IAuthenticationService)).Returns(_authenticationService);
            _authenticationService = Substitute.For<IAuthenticationService>();
            _httpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceProviderStub(_authenticationService),
            };
            _routeConfig = new RouteConfig();
            _authenticationManager = new AuthenticationManager(_options);
        }

        private void SetSuccessfulAuth()
        {
            _authenticationService.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string>())
                .Returns(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(), Scheme)));
        }
        
        private void SetFailedAuth()
        {
            _authenticationService.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string>())
                .Returns(AuthenticateResult.Fail(Scheme));
        }

        private class ServiceProviderStub : IServiceProvider, ISupportRequiredService
        {
            private readonly IAuthenticationService _authenticationService;

            public ServiceProviderStub(IAuthenticationService authenticationService)
            {
                _authenticationService = authenticationService;
            }

            public object GetService(Type serviceType)
                => serviceType == typeof(IAuthenticationService) ? _authenticationService : null;

            public object GetRequiredService(Type serviceType)
                => serviceType == typeof(IAuthenticationService) ? _authenticationService : null;
        }
        
        #endregion
    }
}