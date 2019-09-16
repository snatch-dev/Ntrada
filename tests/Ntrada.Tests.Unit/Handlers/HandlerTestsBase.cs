using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ntrada.Core;

namespace Ntrada.Tests.Unit.Handlers
{
    [ExcludeFromCodeCoverage]
    public abstract class HandlerTestsBase
    {
        protected Task Act() => Handler.HandleAsync(HttpContext.Request, HttpContext.Response, RouteData, RouteConfig);

        protected abstract void get_info_should_return_value();
        
        #region Arrange
        
        protected readonly HttpContext HttpContext;
        protected readonly RouteConfig RouteConfig;
        protected readonly RouteData RouteData;
        protected IHandler Handler;

        protected HandlerTestsBase()
        {
            HttpContext = new DefaultHttpContext();
            RouteConfig = new RouteConfig();
            RouteData = new RouteData();
            InitHandler();
        }

        protected abstract void InitHandler();

        #endregion
    }
}