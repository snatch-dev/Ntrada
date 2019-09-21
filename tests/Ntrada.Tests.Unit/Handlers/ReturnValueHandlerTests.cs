using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Ntrada.Configuration;
using Ntrada.Handlers;
using Shouldly;
using Xunit;

namespace Ntrada.Tests.Unit.Handlers
{
    [ExcludeFromCodeCoverage]
    public class ReturnValueHandlerTests : HandlerTestsBase
    {
        [Fact]
        public async Task handle_should_write_return_value()
        {
            await Act();
        }

        [Fact]
        protected override void get_info_should_return_value()
        {
            RouteConfig.Route = new Route
            {
                ReturnValue = "test"
            };
            var info = Handler.GetInfo(RouteConfig.Route);
            info.ShouldBe($"return a value: '{RouteConfig.Route.ReturnValue}'");
        }

        #region Arrange

        protected override void InitHandler()
        {
            Handler = new ReturnValueHandler();
        }

        #endregion
    }
}