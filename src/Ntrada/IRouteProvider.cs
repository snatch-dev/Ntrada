using System;
using Microsoft.AspNetCore.Routing;

namespace Ntrada
{
    internal interface IRouteProvider
    {
        Action<IRouteBuilder> Build();
    }
}