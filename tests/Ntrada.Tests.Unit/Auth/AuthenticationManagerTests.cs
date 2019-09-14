using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Ntrada.Auth;
using Ntrada.Core;
using Ntrada.Core.Configuration;
using Ntrada.Options;
using Shouldly;
using Xunit;

namespace Ntrada.Tests.Unit.Auth
{
    public class AuthenticationManagerTests
    {
        [Fact]
        public Task<bool> Act() => _authenticationManager.IsAuthenticatedAsync(_httpContext.Request, _routeConfig);

        [Fact]
        public async Task is_authenticated_async_should_return_true_if_global_auth_is_disabled()
        {
            _authenticationManager = new AuthenticationManager(_options);
            var result = await Act();
            result.ShouldBeTrue();
        }

        [Fact]
        public async Task is_authenticated_async_should_return_true_if_route_auth_is_disabled()
        {
            _options.Auth = new Core.Configuration.Auth
            {
                Enabled = true
            };
            _routeConfig.Route = new Route
            {
                Auth = true
            };
            var result = await Act();
            result.ShouldBeTrue();
        }
        
        #region  Arrange

        private IAuthenticationManager _authenticationManager;
        private readonly NtradaOptions _options;
        private HttpContext _httpContext;
        private RouteConfig _routeConfig;

        public AuthenticationManagerTests()
        {
            _options = new NtradaOptions();
            _httpContext = new DefaultHttpContext();
            _routeConfig = new RouteConfig();
            _authenticationManager = new AuthenticationManager(_options);
        }   

        #endregion
    }
}