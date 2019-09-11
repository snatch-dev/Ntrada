using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using Route = Ntrada.Core.Configuration.Route;

namespace Ntrada.Requests
{
    public class PayloadBuilder : IPayloadBuilder
    {
        private static readonly string EmptyContent = "{}";
        private static readonly string ResourceIdProperty = "id";
        private readonly NtradaConfiguration _configuration;
        private readonly IPayloadManager _payloadManager;
        private readonly IValueProvider _valueProvider;
        private readonly IDictionary<string, PayloadSchema> _payloads;

        public PayloadBuilder(NtradaConfiguration configuration, IPayloadManager payloadManager,
            IValueProvider valueProvider)
        {
            _configuration = configuration;
            _payloadManager = payloadManager;
            _valueProvider = valueProvider;
            _payloads = payloadManager.Payloads;
        }

        public async Task<PayloadSchema> BuildAsync(string resourceId, Route route, HttpRequest request, RouteData data)
        {
            var content = string.Empty;
            if (request.Body != null)
            {
                using (var reader = new StreamReader(request.Body))
                {
                    content = await reader.ReadToEndAsync();
                }
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                content = EmptyContent;
            }

            var payloadKey = GetPayloadKey(route);
            var command = _payloads.ContainsKey(payloadKey)
                ? GetObjectFromPayload(route, content)
                : GetObject(content);

            var commandValues = (IDictionary<string, object>) command;
            if (!string.IsNullOrWhiteSpace(resourceId))
            {
                var resourceIdProperty = string.IsNullOrWhiteSpace(route.ResourceId?.Property)
                    ? _configuration.ResourceId.Property
                    : route.ResourceId?.Property;
                if (string.IsNullOrWhiteSpace(resourceIdProperty))
                {
                    resourceIdProperty = ResourceIdProperty;
                }

                commandValues[resourceIdProperty] = resourceId;
            }

            foreach (var setter in route.Bind ?? Enumerable.Empty<string>())
            {
                var keyAndValue = setter.Split(':');
                var key = keyAndValue[0];
                var value = keyAndValue[1];
                commandValues[key] = _valueProvider.Get(value, request, data);
                var routeValue = value.Length > 2 ? value.Substring(1, value.Length - 2) : string.Empty;
                if (data.Values.TryGetValue(routeValue, out var dataValue))
                {
                    commandValues[key] = dataValue;
                }
            }

            foreach (var transformation in route.Transform ?? Enumerable.Empty<string>())
            {
                var beforeAndAfter = transformation.Split(':');
                var before = beforeAndAfter[0];
                var after = beforeAndAfter[1];
                if (!commandValues.TryGetValue(before, out var value))
                {
                    continue;
                }

                commandValues.Remove(before);
                commandValues.Add(after, value);
            }

            _payloads.TryGetValue(payloadKey, out var payloadSchema);

            return new PayloadSchema(command as ExpandoObject, payloadSchema?.Schema);
        }

        private object GetObjectFromPayload(Route route, string content)
        {
            var payloadValue = _payloads[GetPayloadKey(route)].Payload;
            var request = JsonConvert.DeserializeObject(content, payloadValue.GetType());
            var payloadValues = (IDictionary<string, object>) payloadValue;
            var requestValues = (IDictionary<string, object>) request;

            foreach (var key in requestValues.Keys)
            {
                if (!payloadValues.ContainsKey(key))
                {
                    requestValues.Remove(key);
                }
            }

            return request;
        }

        private static object GetObject(string content)
        {
            dynamic payload = new ExpandoObject();
            JsonConvert.PopulateObject(content, payload);

            return JsonConvert.DeserializeObject(content, payload.GetType());
        }

        private string GetPayloadKey(Route route) => _payloadManager.GetKey(route.Method, route.Upstream);
    }
}