using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Ntrada.Hooks
{
    public interface IRequestHook
    {
        Task InvokeAsync(HttpRequest request, ExecutionData data);
    }
}