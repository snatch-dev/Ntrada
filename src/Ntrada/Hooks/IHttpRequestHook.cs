using System.Net.Http;
using System.Threading.Tasks;

namespace Ntrada.Hooks
{
    public interface IHttpRequestHook
    {
        Task InvokeAsync(HttpRequestMessage request, ExecutionData data);
    }
}