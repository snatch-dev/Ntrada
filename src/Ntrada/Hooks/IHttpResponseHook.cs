using System.Net.Http;
using System.Threading.Tasks;

namespace Ntrada.Hooks
{
    public interface IHttpResponseHook
    {
        Task InvokeAsync(HttpResponseMessage response, ExecutionData data);
    }
}