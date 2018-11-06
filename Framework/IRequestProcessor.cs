using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace NGate.Framework
{
    public interface IRequestProcessor
    {
        Task<ExecutionData> ProcessAsync(Route route, HttpRequest request, HttpResponse response, RouteData data);
    }
}