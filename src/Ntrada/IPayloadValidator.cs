using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Ntrada
{
    public interface IPayloadValidator
    {
        Task<bool> TryValidate(ExecutionData executionData, HttpResponse httpResponse);
    }
}