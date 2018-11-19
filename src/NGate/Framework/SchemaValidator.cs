using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NJsonSchema;

namespace NGate.Framework
{
    public class SchemaValidator : ISchemaValidator
    {
        public async Task<IEnumerable<string>> ValidateAsync(string payload, string schema)
        {
            if (string.IsNullOrWhiteSpace(schema))
            {
                return Enumerable.Empty<string>();
            }
            
            var jsonSchema = await JsonSchema4.FromJsonAsync(schema);
            var errors = jsonSchema.Validate(payload);

            return errors.Select(e => e.ToString());
        }
    }
}