using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Ntrada.Core;

namespace Ntrada.Requests
{
    internal sealed class PayloadValidator : IPayloadValidator
    {
        private readonly ISchemaValidator _schemaValidator;

        public PayloadValidator(ISchemaValidator schemaValidator)
        {
            _schemaValidator = schemaValidator;
        }
        
        public async Task<bool> TryValidate(ExecutionData executionData, HttpResponse httpResponse)
        {
            if (executionData.IsPayloadValid)
            {
                return true;
            }

            var response = new {errors = executionData.ValidationErrors};
            var payload = JsonConvert.SerializeObject(response);
            httpResponse.ContentType = "application/json";
            await httpResponse.WriteAsync(payload);

            return false;
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
    }
}