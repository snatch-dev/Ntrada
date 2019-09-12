using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ntrada.Requests;
using Route = Ntrada.Core.Configuration.Route;

namespace Ntrada
{
    internal interface IPayloadBuilder
    {
        bool IsCustom(string resourceId, Route route);
        Task<PayloadSchema> BuildAsync(string resourceId, Route route, HttpRequest request, RouteData data);
    }
}