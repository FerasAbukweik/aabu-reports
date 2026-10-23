using System.Text.Json;

namespace aabu_reports.webapi.Middlewares;

public class GlobalExceptionMiddleWare(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleWare> logger
    )
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var res = new
            {
                message = ex.Message + "|" + ex.InnerException?.Message ?? "no inner exception" // temporary for test
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(res) , context.RequestAborted);
        }
    }
}

public static class GlobalExceptionMiddleWareExtensionMethods
{
    public static IApplicationBuilder UseGlobalExceptionMiddleWare(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleWare>();
    }
}
