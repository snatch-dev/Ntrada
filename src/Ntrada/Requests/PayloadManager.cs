using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ntrada.Core;
using Ntrada.Options;

namespace Ntrada.Requests
{
    internal sealed class PayloadManager : IPayloadManager
    {
        private readonly NtradaOptions _options;
        private readonly ISchemaValidator _schemaValidator;

        public PayloadManager(NtradaOptions options, ISchemaValidator schemaValidator)
        {
            _options = options;
            _schemaValidator = schemaValidator;
            Payloads = LoadPayloads();
        }

        public IDictionary<string, PayloadSchema> Payloads { get; }

        private IDictionary<string, PayloadSchema> LoadPayloads()
        {
            var payloads = new Dictionary<string, PayloadSchema>();
            var modulesPath = _options.ModulesPath;
            modulesPath = string.IsNullOrWhiteSpace(modulesPath)
                ? string.Empty
                : (modulesPath.EndsWith("/") ? modulesPath : $"{modulesPath}/");

            foreach (var module in _options.Modules)
            {
                foreach (var route in module.Value.Routes)
                {
                    if (string.IsNullOrWhiteSpace(route.Payload))
                    {
                        continue;
                    }

                    var payloadsFolder = _options.PayloadsFolder;
                    var fullPath = $"{modulesPath}{module.Value.Name}/{payloadsFolder}/{route.Payload}";
                    var fullJsonPath = fullPath.EndsWith(".json") ? fullPath : $"{fullPath}.json";
                    if (!File.Exists(fullJsonPath))
                    {
                        continue;
                    }

                    var schemaPath = $"{modulesPath}{module.Value.Name}/{payloadsFolder}/{route.Schema}";
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
                    if (!string.IsNullOrWhiteSpace(module.Value.Path))
                    {
                        var modulePath = module.Value.Path.EndsWith("/") ? module.Value.Path : $"{module.Value.Path}/";
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

                    payloads.Add(GetKey(route.Method, upstream), new PayloadSchema(expandoObject, schema));
                }
            }

            return payloads;
        }

        public async Task<IEnumerable<Error>> GetValidationErrorsAsync(PayloadSchema payloadSchema)
        {
            if (string.IsNullOrWhiteSpace(payloadSchema.Schema))
            {
                return Enumerable.Empty<Error>();
            }

            return await _schemaValidator.ValidateAsync(JsonConvert.SerializeObject(payloadSchema.Payload),
                payloadSchema.Schema);
        }

        public string GetKey(string method, string upstream) => $"{method?.ToLowerInvariant()}:{upstream}";
    }
}