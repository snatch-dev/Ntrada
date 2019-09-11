using System.Collections.Generic;
using System.Threading.Tasks;
using Ntrada.Core;

namespace Ntrada
{
    public interface ISchemaValidator
    {
        Task<IEnumerable<Error>> ValidateAsync(string payload, string schema);
    }
}