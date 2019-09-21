using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NJsonSchema;

namespace Ntrada.Requests
{
    internal sealed class SchemaValidator : ISchemaValidator
    {
        public async Task<IEnumerable<Error>> ValidateAsync(string payload, string schema)
        {
            if (string.IsNullOrWhiteSpace(schema))
            {
                return Enumerable.Empty<Error>();
            }

            var jsonSchema = await JsonSchema.FromJsonAsync(schema);
            var errors = jsonSchema.Validate(payload);

            return errors.Select(e => new Error
            {
                Code = e.Kind.ToString(),
                Property = e.Property,
                Message = e.ToString()
            });
        }
    }
}