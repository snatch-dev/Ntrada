using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Route = Ntrada.Configuration.Route;

namespace Ntrada
{
    internal interface IPayloadTransformer
    {
        bool HasTransformations(string resourceId, Route route);
        PayloadSchema Transform(string payload, string resourceId, Route route, HttpRequest request, RouteData data);
    }
}