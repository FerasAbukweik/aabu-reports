using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Notifications;
using AabuReports.Infrastructure;
using AabuReports.Webapi.Hubs.AppHub;
using Microsoft.AspNetCore.SignalR;

namespace AabuReports.Webapi.Services;

public class SignalRNotificationPusher(
    IHubContext<AppHub, IAppClient> hubContext,
    ApplicationDbContext dbContext,
    ILogger<SignalRNotificationPusher> logger) : INotificationPusher
{
    private readonly List<(Guid UserId, NotificationDto Notification)> pending = [];

    public Task PushAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (dbContext.Database.CurrentTransaction is not null)
        {
            pending.Add((userId, notification));
            return Task.CompletedTask;
        }

        return SendAsync(userId, notification);
    }

    public void DiscardPending() => pending.Clear();

    public async Task PublishPendingAsync()
    {
        var notifications = pending.ToArray();
        pending.Clear();

        foreach (var (userId, notification) in notifications)
        {
            try
            {
                await SendAsync(userId, notification);
            }
            catch (Exception exception)
            {
                // The transaction already committed. A failed live push must not fail the saved operation.
                logger.LogWarning(exception, "{ServiceName}.{MethodName} failed to push notification {NotificationId}",
                    nameof(SignalRNotificationPusher), nameof(PublishPendingAsync), notification.Id);
            }
        }
    }

    private Task SendAsync(Guid userId, NotificationDto notification)
    {
        return hubContext.Clients.User(userId.ToString()).ReceiveNotification(notification);
    }
}
