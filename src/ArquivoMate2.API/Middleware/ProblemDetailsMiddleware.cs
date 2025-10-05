using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace ArquivoMate2.API.Middleware
{
    public class ProblemDetailsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ProblemDetailsMiddleware> _logger;

        public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                var pd = new ProblemDetails
                {
                    Title = "Unhandled exception",
                    Detail = ex.Message,
                    Status = (int)HttpStatusCode.InternalServerError
                };

                context.Response.StatusCode = pd.Status.Value;
                context.Response.ContentType = "application/problem+json";
                var json = JsonSerializer.Serialize(pd);
                await context.Response.WriteAsync(json);
            }
        }
    }
}
