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
using Ntrada.Configuration;
using Ntrada.Models;
using Ntrada.Routing;
using Ntrada.Schema;
using Ntrada.Values;

namespace Ntrada.Requests
{
    public class RequestProcessor : IRequestProcessor
    {
        private readonly NtradaConfiguration _configuration;
        private readonly IValueProvider _valueProvider;
        private readonly ISchemaValidator _schemaValidator;
        private readonly IDictionary<string, KeyValuePair<ExpandoObject, string>> _messages;

        public RequestProcessor(NtradaConfiguration configuration, IValueProvider valueProvider,
            ISchemaValidator schemaValidator)
        {
            _configuration = configuration;
            _valueProvider = valueProvider;
            _schemaValidator = schemaValidator;
            _messages = LoadMessages(_configuration);
        }

        public async Task<ExecutionData> ProcessAsync(RouteConfig routeConfig,
            HttpRequest request, HttpResponse response, RouteData data)
        {
            request.Headers.TryGetValue("content-type", out var contentType);
            var contentTypeValue = contentType.ToString().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(contentTypeValue) || contentTypeValue.Contains("text/plain"))
            {
                contentTypeValue = "application/json";
            }

            var requestId = Guid.NewGuid().ToString("N");
            var resourceId = Guid.NewGuid().ToString("N");
            var executionData = new ExecutionData
            {
                RequestId = requestId,
                ResourceId = resourceId,
                Route = routeConfig.Route,
                Request = request,
                Response = response,
                Data = data,
                Downstream = GetDownstream(routeConfig, request, data),
                Payload = await GetPayloadAsync(resourceId, routeConfig.Route, request, data),
                UserId = _valueProvider.Get("@user_id", request, data),
                ContentType = contentTypeValue
            };
            if (_messages.TryGetValue(GetMessagesKey(routeConfig.Route), out var dataAndSchema))
            {
                executionData.ValidationErrors = await GetValidationErrorsAsync(routeConfig.Route,
                    executionData.Payload, dataAndSchema.Value);
            }

            return executionData;
        }

        private async Task<IEnumerable<Error>> GetValidationErrorsAsync(Configuration.Route route,
            ExpandoObject payload, string schema)
        {
            if (string.IsNullOrWhiteSpace(schema))
            {
                return Enumerable.Empty<Error>();
            }

            return await _schemaValidator.ValidateAsync(JsonConvert.SerializeObject(payload), schema);
        }

        private async Task<ExpandoObject> GetPayloadAsync(string resourceId, Configuration.Route route, HttpRequest request,
            RouteData data)
        {
            if (route.Use == "downstream" &&
                (string.IsNullOrWhiteSpace(route.DownstreamMethod) || route.DownstreamMethod == "get"))
            {
                return null;
            }

            var content = "{}";
            if (request.Body != null)
            {
                using (var reader = new StreamReader(request.Body))
                {
                    content = await reader.ReadToEndAsync();
                }
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                content = "{}";
            }

            var command = _messages.ContainsKey(GetMessagesKey(route))
                ? GetObjectFromPayload(route, content)
                : GetObject(content);

            var commandValues = (IDictionary<string, object>) command;
            if (_configuration.ResourceId?.Generate == true && (route.GenerateResourceId != false))
            {
                var resourceIdProperty = _configuration.ResourceId.Property;
                if (string.IsNullOrWhiteSpace(resourceIdProperty))
                {
                    resourceIdProperty = "id";
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
                if (commandValues.TryGetValue(before, out var value))
                {
                    commandValues.Remove(before);
                    commandValues.Add(after, value);
                }
            }

            return command as ExpandoObject;
        }

        private object GetObjectFromPayload(Configuration.Route route, string content)
        {
            var payloadValue = _messages[GetMessagesKey(route)].Key;
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

        private Dictionary<string, KeyValuePair<ExpandoObject, string>> LoadMessages(NtradaConfiguration configuration)
        {
            var messages = new Dictionary<string, KeyValuePair<ExpandoObject, string>>();
            var modulesPath = configuration.ModulesPath;
            modulesPath = string.IsNullOrWhiteSpace(modulesPath)
                ? string.Empty
                : (modulesPath.EndsWith("/") ? modulesPath : $"{modulesPath}/");

            foreach (var module in configuration.Modules)
            {
                foreach (var route in module.Routes)
                {
                    if (string.IsNullOrWhiteSpace(route.Payload))
                    {
                        continue;
                    }

                    var payloadsFolder = configuration.PayloadsFolder;
                    var fullPath = $"{modulesPath}{module.Name}/{payloadsFolder}/{route.Payload}";
                    var fullJsonPath = fullPath.EndsWith(".json") ? fullPath : $"{fullPath}.json";
                    if (!File.Exists(fullJsonPath))
                    {
                        continue;
                    }

                    var schemaPath = $"{modulesPath}{module.Name}/{payloadsFolder}/{route.Schema}";
                    var fullSchemaPath = schemaPath.EndsWith(".json") ? schemaPath : $"{schemaPath}.json";
                    var schema = string.Empty;
                    if (File.Exists(fullSchemaPath))
                    {
                        schema = File.ReadAllText(fullSchemaPath);
                    }

                    var json = File.ReadAllText(fullJsonPath);
                    dynamic expandoObject = new ExpandoObject();
                    JsonConvert.PopulateObject(json, expandoObject);

                    var upstream = string.IsNullOrWhiteSpace(route.Upstream) ? string.Empty : route.Upstream;
                    if (!string.IsNullOrWhiteSpace(module.Path))
                    {
                        var modulePath = module.Path.EndsWith("/") ? module.Path : $"{module.Path}/";
                        if (upstream.StartsWith("/"))
                        {
                            upstream = upstream.Substring(1, upstream.Length - 1);
                        }

                        if (upstream.EndsWith("/"))
                        {
                            upstream = upstream.Substring(0, upstream.Length - 1);
                        }

                        upstream = $"{modulePath}{upstream}";
                    }

                    if (string.IsNullOrWhiteSpace(upstream))
                    {
                        upstream = "/";
                    }

                    messages.Add(GetMessagesKey(route.Method, upstream),
                        new KeyValuePair<ExpandoObject, string>(expandoObject, schema));
                }
            }

            return messages;
        }

        private string GetDownstream(RouteConfig routeConfig, HttpRequest request, RouteData data)
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

            if (_configuration.PassQueryString != false && routeConfig.Route.PassQueryString != false)
            {
                var queryString = request.QueryString.ToString();
                if (downstream.Contains("?") && !string.IsNullOrWhiteSpace(queryString))
                {
                    queryString = $"&{queryString.Substring(1, queryString.Length - 1)}";
                }

                stringBuilder.Append(queryString);
            }

            return stringBuilder.ToString();
        }

        private static string GetMessagesKey(Configuration.Route route)
            => GetMessagesKey(route.Method, route.Upstream);

        private static string GetMessagesKey(string method, string upstream)
            => $"{method?.ToLowerInvariant()}:{upstream}";
    }
}