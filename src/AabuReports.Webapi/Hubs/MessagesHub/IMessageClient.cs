using AabuReports.Core.Dtos.Messages;

namespace AabuReports.Webapi.Hubs.MessagesHub;

/// <summary> the methods the server can call on a connected client (the frontend listens to these names) </summary>
public interface IMessageClient
{
    Task ReceiveMessage(MessageDto message);

    /// <summary> someone else in the chat is typing (the frontend compares the id with its own user id) </summary>
    Task NotifyTyping(Guid userId);

    Task NotifyStoppedTyping(Guid userId);
}
