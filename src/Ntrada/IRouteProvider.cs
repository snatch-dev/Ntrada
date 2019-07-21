using System;
using Microsoft.AspNetCore.Routing;

namespace Ntrada
{
    public interface IRouteProvider
    {
        Action<IRouteBuilder> Build();
    }
}