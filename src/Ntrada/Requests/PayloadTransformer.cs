using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using Ntrada.Options;
using Route = Ntrada.Core.Configuration.Route;

namespace Ntrada.Requests
{
    internal sealed class PayloadTransformer : IPayloadTransformer
    {
        private const string ResourceIdProperty = "id";
        private readonly NtradaOptions _options;
        private readonly IPayloadManager _payloadManager;
        private readonly IValueProvider _valueProvider;
        private readonly IDictionary<string, PayloadSchema> _payloads;
        
        public PayloadTransformer(NtradaOptions options, IPayloadManager payloadManager, IValueProvider valueProvider)
        {
            _options = options;
            _payloadManager = payloadManager;
            _valueProvider = valueProvider;
            _payloads = payloadManager.Payloads;
        }
        
        public bool HasTransformations(string resourceId, Route route)
        {
            if (!string.IsNullOrWhiteSpace(resourceId))
            {
                return true;
            }

            if (route.Bind.IsNotEmpty())
            {
                return true;
            }
            
            return route.Transform.IsNotEmpty() || _payloads.ContainsKey(GetPayloadKey(route));
        }
        
        public PayloadSchema Transform(string payload, string resourceId, Route route, HttpRequest request, RouteData data)
        {
            var payloadKey = GetPayloadKey(route);
            var command = _payloads.ContainsKey(payloadKey)
                ? GetObjectFromPayload(route, payload)
                : GetObject(payload);

            var commandValues = (IDictionary<string, object>) command;
            if (!string.IsNullOrWhiteSpace(resourceId))
            {
                var resourceIdProperty = string.IsNullOrWhiteSpace(route.ResourceId?.Property)
                    ? _options.ResourceId.Property
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