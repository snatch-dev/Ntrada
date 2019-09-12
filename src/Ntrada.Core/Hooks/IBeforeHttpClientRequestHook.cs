using System.Net.Http;

namespace Ntrada.Core.Hooks
{
    public interface IBeforeHttpClientRequestHook
    {
        void Invoke(HttpClient client, ExecutionData data);
    }
}