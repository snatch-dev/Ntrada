using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace NGate.Framework
{
    public interface IValueProvider
    {
        string Get(string value, HttpRequest request, RouteData data);
    }
}