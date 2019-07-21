using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ntrada.Requests
{
    public class ValueProvider : IValueProvider
    {
        private static readonly string[] AvailableTokens = {"user_id"};

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