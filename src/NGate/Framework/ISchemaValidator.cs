using System.Collections.Generic;
using System.Threading.Tasks;

namespace NGate.Framework
{
    public interface ISchemaValidator
    {
        Task<IEnumerable<Error>> ValidateAsync(string payload, string schema);
    }
}