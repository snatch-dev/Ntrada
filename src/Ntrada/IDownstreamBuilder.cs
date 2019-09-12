using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ntrada.Core;

namespace Ntrada
{
    internal interface IDownstreamBuilder
    {
        string GetDownstream(RouteConfig routeConfig, HttpRequest request, RouteData data);
    }
}