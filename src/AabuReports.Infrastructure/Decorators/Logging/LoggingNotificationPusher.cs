using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Notifications;
using AabuReports.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Decorators.Logging;

/// <summary>
/// logs the failed pushes of the real time notifications and rethrows,
/// <see cref="NotificationService"/> swallows the exception because the notification is already saved
/// </summary>
public class LoggingNotificationPusher(
    INotificationPusher innerPusher,
    ILogger<NotificationService> logger) : INotificationPusher
{
    private const string Name = "NotificationPusher";

    public async Task PushAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await innerPusher.PushAsync(userId, notification, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "{ServiceName}.{MethodName} failed to push notification {NotificationId} to user {UserId}",
                Name, nameof(PushAsync), notification.Id, userId);
            throw;
        }
    }
}
