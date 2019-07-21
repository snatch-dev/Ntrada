using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Route = Ntrada.Configuration.Route;

namespace Ntrada
{
    public interface IPayloadBuilder
    {
        Task<PayloadSchema> BuildAsync(string resourceId, Route route, HttpRequest request, RouteData data);
    }
}