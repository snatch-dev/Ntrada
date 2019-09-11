using System.Net.Http;
using Ntrada.Core;

namespace Ntrada.Hooks
{
    public interface IBeforeHttpClientRequestHook
    {
        void Invoke(HttpClient client, ExecutionData data);
    }
}