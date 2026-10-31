using AabuReports.Core.Dtos.Messages;
using AabuReports.Core.Dtos.Notifications;

namespace AabuReports.Webapi.Hubs.AppHub;

/// <summary> the methods the server can call on a connected client (the frontend listens to these names) </summary>
public interface IAppClient
{
    // notifications (sent to one user, through all of their connections)

    Task ReceiveNotification(NotificationDto notification);

    // chat (sent to the group of a report, the group name is the report id)

    Task ReceiveMessage(MessageDto message);

    /// <summary> someone else in the chat of the report is typing (the frontend compares the user id with its own) </summary>
    Task NotifyTyping(Guid reportId, Guid userId);

    Task NotifyStoppedTyping(Guid reportId, Guid userId);
}
