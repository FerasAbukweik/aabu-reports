using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AabuReports.Webapi.Hubs.NotificationHub;

/// <summary>
/// server to client only: the server pushes notifications through <see cref="INotificationClient"/>, the clients send nothing.
/// Only logged in users can connect, and each user only receives their own notifications.
/// </summary>
[Authorize]
public class NotificationHub : Hub<INotificationClient>
{
}
