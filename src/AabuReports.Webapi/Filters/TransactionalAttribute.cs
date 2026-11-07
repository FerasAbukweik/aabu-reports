using AabuReports.Infrastructure;
using AabuReports.Webapi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AabuReports.Webapi.Filters;

public class TransactionalAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // DI database
        var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

        var notificationPusher = context.HttpContext.RequestServices.GetRequiredService<SignalRNotificationPusher>();
        var cancellationToken = context.HttpContext.RequestAborted;
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var executedContext = await next();

                if (executedContext.Exception != null || IsFailureResult(executedContext.Result))
                {
                    notificationPusher.DiscardPending();
                    await transaction.RollbackAsync(CancellationToken.None);
                    return;
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception)
            {
                notificationPusher.DiscardPending();
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }

            await notificationPusher.PublishPendingAsync();
        });
    }
    
    
    private static bool IsFailureResult(IActionResult? result)
    {
        if (result == null) return false;

        // Handles standard status-bearing results like BadRequestObjectResult, ObjectResult, etc.
        if (result is IStatusCodeActionResult statusCodeResult)
        {
            var statusCode = statusCodeResult.StatusCode;
            return statusCode.HasValue && statusCode.Value >= 400;
        }

        // Fallback for custom ObjectResults that don't implement the interface directly
        if (result is ObjectResult objectResult)
        {
            return objectResult.StatusCode.HasValue && objectResult.StatusCode.Value >= 400;
        }

        return false;
    }
}
