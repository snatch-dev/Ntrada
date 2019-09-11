using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ntrada.Core;

namespace Ntrada.Routing
{
    public class DownstreamBuilder : IDownstreamBuilder
    {
        private readonly NtradaConfiguration _configuration;
        private readonly IValueProvider _valueProvider;

        public DownstreamBuilder(NtradaConfiguration configuration, IValueProvider valueProvider)
        {
            _configuration = configuration;
            _valueProvider = valueProvider;
        }
        
        public string GetDownstream(RouteConfig routeConfig, HttpRequest request, RouteData data)
        {
            if (string.IsNullOrWhiteSpace(routeConfig.Downstream))
            {
                return null;
            }

            var stringBuilder = new StringBuilder();
            var downstream = routeConfig.Downstream;
            stringBuilder.Append(downstream);
            if (downstream.Contains("@"))
            {
                foreach (var token in _valueProvider.Tokens)
                {
                    var tokenName = $"@{token}";
                    stringBuilder.Replace(tokenName, _valueProvider.Get(tokenName, request, data));
                }
            }

            foreach (var value in data.Values)
            {
                stringBuilder.Replace($"{{{value.Key}}}", value.Value.ToString());
            }

            if (_configuration.PassQueryString == false || routeConfig.Route.PassQueryString == false)
            {
                return stringBuilder.ToString();
            }

            var queryString = request.QueryString.ToString();
            if (downstream.Contains("?") && !string.IsNullOrWhiteSpace(queryString))
            {
                queryString = $"&{queryString.Substring(1, queryString.Length - 1)}";
            }

            stringBuilder.Append(queryString);

            return stringBuilder.ToString();
        }
    }
}