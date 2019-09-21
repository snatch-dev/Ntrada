using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Ntrada.Requests
{
    internal sealed class PayloadBuilder : IPayloadBuilder
    {
        public async Task<string> BuildRawAsync(HttpRequest request)
        {
            var content = string.Empty;
            if (request.Body == null)
            {
                return content;
            }

            using (var reader = new StreamReader(request.Body))
            {
                content = await reader.ReadToEndAsync();
            }

            return content;
        }

        public async Task<T> BuildJsonAsync<T>(HttpRequest request) where T : class, new()
        {
            var payload = await BuildRawAsync(request);

            return string.IsNullOrWhiteSpace(payload) ? new T() : JsonConvert.DeserializeObject<T>(payload);
        }
    }
}