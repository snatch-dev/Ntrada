using System.Collections.Generic;
using System.Threading.Tasks;
using Ntrada.Models;

namespace Ntrada.Schema
{
    public interface ISchemaValidator
    {
        Task<IEnumerable<Error>> ValidateAsync(string payload, string schema);
    }
}