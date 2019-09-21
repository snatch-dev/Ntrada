using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Ntrada
{
    public interface IPayloadBuilder
    {
        Task<string> BuildRawAsync(HttpRequest request);
        Task<T> BuildJsonAsync<T>(HttpRequest request) where T : class, new();
    }
}