using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;

namespace NGate.Framework
{
    public class RequestProcessor : IRequestProcessor
    {
        private readonly Configuration _configuration;
        private readonly IValueProvider _valueProvider;
        private readonly IDictionary<string, ExpandoObject> _messages;

        public RequestProcessor(Configuration configuration, IValueProvider valueProvider)
        {
            _configuration = configuration;
            _valueProvider = valueProvider;
            _messages = LoadMessages(_configuration);
        }

        public async Task<ExecutionData> ProcessAsync(RouteConfig routeConfig,
            HttpRequest request, HttpResponse response, RouteData data)
        {
            request.Headers.TryGetValue("content-type", out var contentType);
            var resourceId = Guid.NewGuid().ToString();
            var executionData = new ExecutionData
            {
                ResourceId = resourceId,
                Route = routeConfig.Route,
                Request = request,
                Response = response,
                Data = data,
                Downstream = GetDownstream(routeConfig, request, data),
                Payload = await GetPayloadAsync(resourceId, routeConfig.Route, request, data),
                UserId = _valueProvider.Get("{user_id}", request, data),
                ContentType = contentType
            };

            return executionData;
        }

        private async Task<ExpandoObject> GetPayloadAsync(string resourceId, Route route, HttpRequest request,
            RouteData data)
        {
            if (route.Method == "get" || route.Method == "delete" || route.Method == "options")
            {
                return null;
            }

            using (var reader = new StreamReader(request.Body))
            {
                var content = await reader.ReadToEndAsync();
                var command = _messages.ContainsKey(route.Upstream)
                    ? GetObjectFromPayload(route, content)
                    : GetObject(content);


                var commandValues = (IDictionary<string, object>) command;
                foreach (var setter in route.Set ?? Enumerable.Empty<string>())
                {
                    var keyAndValue = setter.Split(':');
                    var key = keyAndValue[0];
                    var value = keyAndValue[1];
                    commandValues[key] = _valueProvider.Get(value, request, data);
                }

                foreach (var transformation in route.Transform ?? Enumerable.Empty<string>())
                {
                    var beforeAndAfter = transformation.Split(':');
                    var before = beforeAndAfter[0];
                    var after = beforeAndAfter[1];
                    if (commandValues.TryGetValue(before, out var value))
                    {
                        commandValues.Remove(before);
                        commandValues.Add(after, value);
                    }
                }

                if (_configuration.Config.GenerateResourceId && (route.GenerateResourceId != false))
                {
                    commandValues.Add("id", resourceId);
                }

                return command as ExpandoObject;
            }
        }

        private object GetObjectFromPayload(Route route, string content)
        {
            var payloadValue = _messages[route.Upstream];
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

        private object GetObject(string content)
        {
            dynamic payload = new ExpandoObject();
            JsonConvert.PopulateObject(content, payload);

            return JsonConvert.DeserializeObject(content, payload.GetType());
        }

        private Dictionary<string, ExpandoObject> LoadMessages(Configuration configuration)
        {
            var messages = new Dictionary<string, ExpandoObject>();
            var payloadsPath = configuration.Config.PayloadsPath;
            var fullPayloadsPath = string.IsNullOrWhiteSpace(payloadsPath)
                ? string.Empty
                : (payloadsPath.EndsWith("/") ? payloadsPath : $"{payloadsPath}/");
            foreach (var route in configuration.Routes.SelectMany(r => r.Value))
            {
                if (string.IsNullOrWhiteSpace(route.Payload))
                {
                    continue;
                }

                var filePath = $"{fullPayloadsPath}{route.Payload}";
                if (!File.Exists(filePath))
                {
                    continue;
                }

                var json = File.ReadAllText(filePath);
                dynamic expandoObject = new ExpandoObject();
                JsonConvert.PopulateObject(json, expandoObject);
                messages.Add(route.Upstream, expandoObject);
            }

            return messages;
        }

        private string GetDownstream(RouteConfig routeConfig, HttpRequest request, RouteData data)
        {
            if (string.IsNullOrWhiteSpace(routeConfig.Downstream))
            {
                return null;
            }

            var downstream = routeConfig.Downstream;
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(downstream);
            foreach (var value in data.Values)
            {
                stringBuilder.Replace($"{{{value.Key}}}", value.Value.ToString());
            }

            if (_configuration.Config.PassQueryString != false && routeConfig.Route.PassQueryString != false)
            {
                stringBuilder.Append(request.QueryString.ToString());
            }

            return stringBuilder.ToString();
        }
    }
}