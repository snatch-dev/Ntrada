using System.Collections.Generic;
using System.Threading.Tasks;
using Ntrada.Core;

namespace Ntrada
{
    public interface IPayloadManager
    {
        string GetKey(string method, string upstream);
        IDictionary<string, PayloadSchema> Payloads { get; }
        Task<IEnumerable<Error>> GetValidationErrorsAsync(PayloadSchema payloadSchema);
    }
}