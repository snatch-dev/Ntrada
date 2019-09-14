using System;
using System.Collections.Generic;
using System.Linq;
using Ntrada.Core.Configuration;
using Ntrada.Options;

namespace Ntrada.Auth
{
    internal sealed class PolicyManager : IPolicyManager
    {
        private readonly IDictionary<string, Dictionary<string, string>> _policies;
        private readonly NtradaOptions _options;

        public PolicyManager(NtradaOptions options)
        {
            _options = options;
            _policies = LoadPolicies();
        }

        public IDictionary<string, string> GetClaims(string policy)
            => _policies.TryGetValue(policy, out var claims) ? claims : null;

        private IDictionary<string, Dictionary<string, string>> LoadPolicies()
        {
            var policies = (_options.Auth?.Policies ?? new Dictionary<string, Policy>())
                .ToDictionary(p => p.Key, p => p.Value.Claims.ToDictionary(c => c.Key, c => c.Value));
            VerifyPolicies(policies);

            return policies;
        }

        private void VerifyPolicies(IDictionary<string, Dictionary<string, string>> policies)
        {
            var definedPolicies = (_options.Modules ?? new Dictionary<string, Module>())
                .Select(m => m.Value)
                .SelectMany(m => m.Routes ?? Enumerable.Empty<Route>())
                .SelectMany(r => r.Policies ?? Enumerable.Empty<string>())
                .Distinct();
            var missingPolicies = definedPolicies
                .Except(policies.Select(p => p.Key))
                .ToArray();
            if (missingPolicies.Any())
            {
                throw new Exception($"Missing policies: '{string.Join(", ", missingPolicies)}'");
            }
        }
    }
}