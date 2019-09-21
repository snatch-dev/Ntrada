using System.Net.Http;

namespace Ntrada.Hooks
{
    public interface IBeforeHttpClientRequestHook
    {
        void Invoke(HttpClient client, ExecutionData data);
    }
}