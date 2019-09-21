using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ntrada.Options;

namespace Ntrada.Routing
{
    internal sealed class DownstreamBuilder : IDownstreamBuilder
    {
        private readonly NtradaOptions _options;
        private readonly IValueProvider _valueProvider;

        public DownstreamBuilder(NtradaOptions options, IValueProvider valueProvider)
        {
            _options = options;
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

            if (_options.PassQueryString == false || routeConfig.Route.PassQueryString == false)
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