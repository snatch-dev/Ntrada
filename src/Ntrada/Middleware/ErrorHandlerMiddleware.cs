using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Ntrada.Middleware
{
    public class ErrorHandlerMiddleware : IMiddleware
    {
        private readonly ILogger<ErrorHandlerMiddleware> _logger;

        public ErrorHandlerMiddleware(ILogger<ErrorHandlerMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, exception.Message);
                await HandleErrorAsync(context, exception);
            }
        }

        private static Task HandleErrorAsync(HttpContext context, Exception exception)
        {
            var errorCode = "error";
            var statusCode = HttpStatusCode.BadRequest;
            var message = exception.Message;
            var response = new
            {
                errors = new[]
                {
                    new Error
                    {
                        Code = errorCode,
                        Message = message,
                    }
                }
            };
            var payload = JsonConvert.SerializeObject(response);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int) statusCode;

            return context.Response.WriteAsync(payload);
        }
    }
}