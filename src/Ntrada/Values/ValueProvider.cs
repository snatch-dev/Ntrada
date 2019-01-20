using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ntrada.Values
{
    public class ValueProvider : IValueProvider
    {
        private static readonly string[] AvailableTokens = new[] {"user_id"};

        public IEnumerable<string> Tokens => AvailableTokens;

        public string Get(string value, HttpRequest request, RouteData data)
        {
            switch ($"{value?.ToLowerInvariant()}")
            {
                case "@user_id": return request.HttpContext?.User?.Identity?.Name;
                default: return value;
            }
        }
    }
}