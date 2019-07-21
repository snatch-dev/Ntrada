using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Ntrada.Requests
{
    public class PayloadValidator : IPayloadValidator
    {
        public async Task<bool> TryValidate(ExecutionData executionData, HttpResponse httpResponse)
        {
            if (executionData.IsPayloadValid)
            {
                return true;
            }

            var response = new {errors = executionData.ValidationErrors};
            var payload = JsonConvert.SerializeObject(response);
            httpResponse.ContentType = "application/json";
            await httpResponse.WriteAsync(payload);

            return false;
        }
    }
}