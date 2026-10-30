using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Notifications;
using AabuReports.Webapi.Hubs;
using AabuReports.Webapi.Hubs.NotificationHub;
using Microsoft.AspNetCore.SignalR;

namespace AabuReports.Webapi.Services;

public class SignalRNotificationPusher(
    IHubContext<NotificationHub, INotificationClient> hubContext
    ) : INotificationPusher
{
    public Task PushAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        // SignalR's user id is the NameIdentifier claim, which the access token sets to the user's Guid
        return hubContext.Clients.User(userId.ToString()).ReceiveNotification(notification);
    }
}
