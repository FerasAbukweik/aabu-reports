using System.Text.Json;

namespace AabuReports.Webapi.Middlewares;

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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; there is no response left to write.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ServiceName}.{MethodName} unhandled request error",
                nameof(GlobalExceptionMiddleWare), nameof(InvokeAsync));
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var res = new
            {
                message = "An unexpected error occurred. Please try again."
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
