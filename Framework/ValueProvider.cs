using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace NGate.Framework
{
    public class ValueProvider : IValueProvider
    {
        public string Get(string value, HttpRequest request, RouteData data)
        {
            switch ($"{value?.ToLowerInvariant()}")
            {
                case "{user_id}": return request.HttpContext?.User?.Identity?.Name;
                default: return value;
            }
        }
    }
}