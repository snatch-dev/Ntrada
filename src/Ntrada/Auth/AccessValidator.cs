using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Ntrada.Core;
using Ntrada.Core.Configuration;
using Ntrada.Options;

namespace Ntrada.Auth
{
    internal sealed class AccessValidator : IAccessValidator
    {
        private readonly IDictionary<string, string> _claims;
        private readonly IDictionary<string, Dictionary<string, string>> _policies;
        private readonly NtradaOptions _options;

        public AccessValidator(NtradaOptions options)
        {
            _options = options;
            _claims = options.Auth?.Claims ?? new Dictionary<string, string>();
            _policies = GetPolicies();
            VerifyPolicies();
        }

        private Dictionary<string, Dictionary<string, string>> GetPolicies()
            => (_options.Auth?.Policies ?? new Dictionary<string, Policy>())
                .ToDictionary(p => p.Key, p => p.Value.Claims.ToDictionary(c => GetClaimKey(c.Key), c => c.Value));

        private void VerifyPolicies()
        {
            var definedPolicies = _options.Modules
                .Select(m => m.Value)
                .SelectMany(m => m.Routes ?? Enumerable.Empty<Route>())
                .SelectMany(r => r.Policies ?? Enumerable.Empty<string>())
                .Distinct();
            var missingPolicies = definedPolicies
                .Except(_policies.Select(p => p.Key))
                .ToArray();
            if (missingPolicies.Any())
            {
                throw new Exception($"Missing policies: '{string.Join(", ", missingPolicies)}'");
            }
        }

        private string GetClaimKey(string claim)
            => _claims.TryGetValue(claim, out var value) ? value : claim;

        public async Task<bool> IsAuthenticatedAsync(HttpRequest request, RouteConfig routeConfig)
        {
            if (_options.Auth?.Global != true
                || routeConfig.Route.Auth.HasValue && routeConfig.Route.Auth == false)
            {
                return true;
            }

            var result = await request.HttpContext.AuthenticateAsync();

            return result.Succeeded;
        }

        public bool IsAuthorized(ClaimsPrincipal user, RouteConfig routeConfig)
            => HasClaims(user, routeConfig.Claims) && HasPolicies(user, routeConfig.Route.Policies);

        private bool HasPolicies(ClaimsPrincipal user, IEnumerable<string> policies)
            => policies?.All(p => HasPolicy(user, p)) == true;

        private bool HasPolicy(ClaimsPrincipal user, string policy)
            => HasClaims(user, _policies[policy]);

        private static bool HasClaims(ClaimsPrincipal user, IDictionary<string, string> claims)
            => claims?.All(claim => user.HasClaim(claim.Key, claim.Value)) == true;
    }
}