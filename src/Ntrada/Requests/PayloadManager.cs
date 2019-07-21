using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Ntrada.Requests
{
    public class PayloadManager : IPayloadManager
    {
        private readonly NtradaConfiguration _configuration;
        private readonly ISchemaValidator _schemaValidator;

        public PayloadManager(NtradaConfiguration configuration, ISchemaValidator schemaValidator)
        {
            _configuration = configuration;
            _schemaValidator = schemaValidator;
            Payloads = LoadPayloads();
        }

        public IDictionary<string, PayloadSchema> Payloads { get; }

        private IDictionary<string, PayloadSchema> LoadPayloads()
        {
            var payloads = new Dictionary<string, PayloadSchema>();
            var modulesPath = _configuration.ModulesPath;
            modulesPath = string.IsNullOrWhiteSpace(modulesPath)
                ? string.Empty
                : (modulesPath.EndsWith("/") ? modulesPath : $"{modulesPath}/");

            foreach (var module in _configuration.Modules)
            {
                foreach (var route in module.Routes)
                {
                    if (string.IsNullOrWhiteSpace(route.Payload))
                    {
                        continue;
                    }

                    var payloadsFolder = _configuration.PayloadsFolder;
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