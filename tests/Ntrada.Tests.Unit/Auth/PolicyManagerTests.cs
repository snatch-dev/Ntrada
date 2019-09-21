using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Ntrada.Auth;
using Ntrada.Configuration;
using Ntrada.Options;
using Shouldly;
using Xunit;

namespace Ntrada.Tests.Unit.Auth
{
    [ExcludeFromCodeCoverage]
    public class PolicyManagerTests
    {
        private IDictionary<string, string> Act(string policy) => _policyManager.GetClaims(policy);

        [Fact]
        public void get_claims_should_be_null_if_policies_are_not_defined()
        {
            var result = Act(Policy1);
            result.ShouldBeNull();
        }

        [Fact]
        public void get_claims_should_be_null_if_policy_does_not_exist()
        {
            _options.Auth = new Configuration.Auth
            {
                Policies = _policies
            };
            _policyManager = new PolicyManager(_options);
            var result = Act(MissingPolicy);
            result.ShouldBeNull();
        }

        [Fact]
        public void get_claims_should_not_be_empty_if_policy_exists()
        {
            _options.Auth = new Configuration.Auth
            {
                Policies = _policies
            };
            _policyManager = new PolicyManager(_options);
            var result = Act(Policy1);
            result.ShouldNotBeEmpty();
        }

        [Fact]
        public void get_claims_should_not_be_empty_if_policy_exists_and_is_used_in_routes()
        {
            _options.Auth = new Configuration.Auth
            {
                Policies = _policies
            };
            SetModulesWithPolicies(new[] {Policy1, Policy2});
            _policyManager = new PolicyManager(_options);
            var result = Act(Policy1);
            result.ShouldNotBeEmpty();
        }

        [Fact]
        public void get_claims_should_throw_an_exception_if_policy_used_in_route_was_not_defined()
        {
            _options.Auth = new Configuration.Auth
            {
                Policies = _policies
            };
            SetModulesWithPolicies(new[] {Policy1, Policy2, MissingPolicy});
            var exception = Record.Exception(() => _policyManager = new PolicyManager(_options));
            exception.ShouldNotBeNull();
            exception.ShouldBeOfType<InvalidOperationException>();
        }

        #region Arrange

        private const string Policy1 = "policy1";
        private const string Policy2 = "policy2";
        private const string MissingPolicy = "policy3";

        private readonly IDictionary<string, Policy> _policies =
            new Dictionary<string, Policy>
            {
                [Policy1] = new Policy
                {
                    Claims = new Dictionary<string, string>
                    {
                        ["claim1"] = "value1"
                    }
                },
                [Policy2] = new Policy
                {
                    Claims = new Dictionary<string, string>
                    {
                        ["claim2"] = "value2",
                        ["claim3"] = "value3"
                    }
                }
            };

        private IPolicyManager _policyManager;
        private NtradaOptions _options;

        public PolicyManagerTests()
        {
            _options = new NtradaOptions();
            _policyManager = new PolicyManager(_options);
        }

        private void SetModulesWithPolicies(IEnumerable<string> policies)
        {
            _options.Modules = new Dictionary<string, Module>
            {
                ["module1"] = new Module
                {
                    Routes = new[]
                    {
                        new Route
                        {
                            Policies = policies
                        }
                    }
                }
            };
        }

        #endregion
    }
}