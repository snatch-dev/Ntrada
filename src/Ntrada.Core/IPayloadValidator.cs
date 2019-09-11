using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Ntrada.Core
{
    public interface IPayloadValidator
    {
        Task<bool> TryValidate(ExecutionData executionData, HttpResponse httpResponse);
    }
}