using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Ntrada.Core
{
    public interface IPayloadValidator
    {
        Task<bool> TryValidate(ExecutionData executionData, HttpResponse httpResponse);
        Task<IEnumerable<Error>> GetValidationErrorsAsync(PayloadSchema payloadSchema);
    }
}