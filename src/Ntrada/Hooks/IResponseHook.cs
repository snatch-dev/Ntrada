using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Ntrada.Hooks
{
    public interface IResponseHook
    {
        Task InvokeAsync(HttpResponse response, ExecutionData data);
    }
}