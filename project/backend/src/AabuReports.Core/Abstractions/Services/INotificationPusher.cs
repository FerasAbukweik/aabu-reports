using AabuReports.Core.Dtos.Notifications;

namespace AabuReports.Core.Abstractions.Services;

/// <summary>
/// pushes a notification to the user's open connections in real time (implemented with SignalR in the web api project,
/// so Core and Infrastructure dont depend on SignalR)
/// </summary>
public interface INotificationPusher
{
    /// <summary>
    /// sends the notification to every connection of the user, does nothing if the user is not connected
    /// </summary>
    Task PushAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);
}
