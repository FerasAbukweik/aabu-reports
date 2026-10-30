using AabuReports.Core.Dtos.Notifications;

namespace AabuReports.Webapi.Hubs.NotificationHub;

/// <summary> the methods the server can call on a connected client (the frontend listens to "ReceiveNotification") </summary>
public interface INotificationClient
{
    Task ReceiveNotification(NotificationDto notification);
}
