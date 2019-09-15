using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Claims;
using NSubstitute;
using Ntrada.Auth;
using Ntrada.Core;
using Ntrada.Core.Configuration;
using Shouldly;
using Xunit;

namespace Ntrada.Tests.Unit.Auth
{
    [ExcludeFromCodeCoverage]
    public class AuthorizationManagerTests
    {
        private bool Act() => _authorizationManager.IsAuthorized(_user, _routeConfig);

        [Fact]
        public void is_authorized_should_return_false_if_user_is_null()
        {
            _user = null;
            var result = Act();
            result.ShouldBeFalse();
            _policyManager.DidNotReceiveWithAnyArgs().GetClaims(null);
        }

        [Fact]
        public void is_authorized_should_return_true_if_route_config_route_and_claims_are_null()
        {
            var result = Act();
            result.ShouldBeTrue();
            _policyManager.DidNotReceiveWithAnyArgs().GetClaims(null);
        }

        [Fact]
        public void is_authorized_should_return_true_if_route_config_route_policies_is_null()
        {
            _routeConfig.Route = new Route();
            var result = Act();
            result.ShouldBeTrue();
            _policyManager.DidNotReceiveWithAnyArgs().GetClaims(null);
        }

        [Fact]
        public void is_authorized_should_return_true_if_route_config_route_claims_is_null()
        {
            _routeConfig.Route = new Route
            {
                Policies = Enumerable.Empty<string>()
            };
            var result = Act();
            result.ShouldBeTrue();
            _policyManager.DidNotReceiveWithAnyArgs().GetClaims(null);
        }

        [Fact]
        public void is_authorized_should_return_false_if_user_has_no_policies()
        {
            _routeConfig.Route = new Route
            {
                Policies = _policies
            };
            var result = Act();
            result.ShouldBeFalse();
            _policyManager.Received().GetClaims(_policies.ElementAt(0));
            foreach (var policy in _policies.Skip(1))
            {
                _policyManager.DidNotReceive().GetClaims(policy);
            }
        }

        [Fact]
        public void is_authorized_should_return_false_if_user_has_some_policies()
        {
            _routeConfig.Route = new Route
            {
                Policies = _policies
            };
            SetClaimsIdentity(skipClaims: 1);
            var result = Act();
            result.ShouldBeFalse();
            _policyManager.Received().GetClaims(_policies.ElementAt(0));
            foreach (var policy in _policies.Skip(1))
            {
                _policyManager.DidNotReceive().GetClaims(policy);
            }
        }

        [Fact]
        public void is_authorized_should_return_true_if_user_has_all_policies()
        {
            _routeConfig.Route = new Route
            {
                Policies = _policies
            };
            SetClaimsIdentity();
            var result = Act();
            result.ShouldBeTrue();
            foreach (var policy in _policies)
            {
                _policyManager.Received().GetClaims(policy);
            }
        }

        [Fact]
        public void is_authorized_should_return_false_if_user_has_no_claims()
        {
            _routeConfig.Route = new Route
            {
                Claims = _claims
            };
            var result = Act();
            result.ShouldBeFalse();
            _policyManager.DidNotReceiveWithAnyArgs().GetClaims(null);
        }

        [Fact]
        public void is_authorized_should_return_false_if_user_has_some_claims()
        {
            _routeConfig.Route = new Route
            {
                Claims = _claims
            };
            SetClaimsIdentity(skipClaims: 1);
            var result = Act();
            result.ShouldBeFalse();
            _policyManager.DidNotReceiveWithAnyArgs().GetClaims(null);
        }

        [Fact]
        public void is_authorized_should_return_true_if_user_has_all_claims()
        {
            _routeConfig.Route = new Route
            {
                Claims = _claims
            };
            SetClaimsIdentity();
            var result = Act();
            result.ShouldBeTrue();
            _policyManager.DidNotReceiveWithAnyArgs().GetClaims(null);
        }

        [Fact]
        public void is_authorized_should_return_true_if_user_has_all_claims_and_all_policies()
        {
            _routeConfig.Route = new Route
            {
                Claims = _claims,
                Policies = _policies
            };
            SetClaimsIdentity();
            var result = Act();
            result.ShouldBeTrue();
            foreach (var policy in _policies)
            {
                _policyManager.Received().GetClaims(policy);
            }
        }

        [Fact]
        public void is_authorized_should_return_true_if_user_has_some_claims_and_some_policies()
        {
            _routeConfig.Route = new Route
            {
                Claims = _claims,
                Policies = _policies
            };
            SetClaimsIdentity(1);
            var result = Act();
            result.ShouldBeFalse();
            _policyManager.Received().GetClaims(_policies.ElementAt(0));
            foreach (var policy in _policies.Skip(1))
            {
                _policyManager.DidNotReceive().GetClaims(policy);
            }
        }

        #region  Arrange

        private readonly IEnumerable<string> _policies = new[] {"policy1", "policy2", "policy3"};

        private readonly IDictionary<string, string> _claims = new Dictionary<string, string>
        {
            ["claim1"] = "value1",
            ["claim2"] = "value2",
            ["claim3"] = "value3"
        };

        private readonly IAuthorizationManager _authorizationManager;
        private readonly IPolicyManager _policyManager;
        private ClaimsPrincipal _user;
        private readonly RouteConfig _routeConfig;

        public AuthorizationManagerTests()
        {
            _policyManager = Substitute.For<IPolicyManager>();
            foreach (var policy in _policies)
            {
                _policyManager.GetClaims(policy).Returns(_claims);
            }

            _user = new ClaimsPrincipal();
            _routeConfig = new RouteConfig();
            _authorizationManager = new AuthorizationManager(_policyManager);
        }

        private void SetClaimsIdentity(int skipClaims = 0)
            => _user = new ClaimsPrincipal(new ClaimsPrincipal(new ClaimsIdentity(GetClaims().Skip(skipClaims))));

        private IEnumerable<Claim> GetClaims() => _claims.Select(c => new Claim(c.Key, c.Value));

        #endregion
    }
}