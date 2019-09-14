using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Ntrada.Core;

namespace Ntrada.Auth
{
    internal sealed class AuthorizationManager : IAuthorizationManager
    {
        private readonly IPolicyManager _policyManager;

        public AuthorizationManager(IPolicyManager policyManager)
        {
            _policyManager = policyManager;
        }

        public bool IsAuthorized(ClaimsPrincipal user, RouteConfig routeConfig)
            => HasClaims(user, routeConfig.Claims) && HasPolicies(user, routeConfig.Route.Policies);

        private bool HasPolicies(ClaimsPrincipal user, IEnumerable<string> policies)
            => policies?.All(p => HasPolicy(user, p)) == true;

        private bool HasPolicy(ClaimsPrincipal user, string policy)
            => HasClaims(user, _policyManager.GetClaims(policy));

        private static bool HasClaims(ClaimsPrincipal user, IDictionary<string, string> claims)
            => claims?.All(claim => user.HasClaim(claim.Key, claim.Value)) == true;
    }
}