using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Webapi.ExtensionMethods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AabuReports.Webapi.Hubs.AppHub;

/// <summary>
/// the one hub of the app, the client keeps ONE connection open for as long as the user is logged in.
/// <list type="bullet">
/// <item> notifications: the server pushes them to the user (<see cref="IAppClient.ReceiveNotification"/>), the client sends nothing </item>
/// <item> chat: the client joins the group of a report (<see cref="AddToGroup"/>) when it opens the chat and leaves it
/// (<see cref="RemoveFromGroup"/>) when it closes it. The group name is the report id. Every logged in user can join
/// the chat of any existing report. </item>
/// </list>
/// </summary>
[Authorize]
public class AppHub(IMessageService messageService, IReportRepository reportRepository) : Hub<IAppClient>
{
    /// <summary>
    /// joins the current connection to the group (the chat) of a report, so it receives the messages and the typing
    /// events of that report. Joining twice is harmless. The groups of a connection are lost when it drops, so the
    /// client has to join again after a reconnect.
    /// </summary>
    public async Task AddToGroup(Guid groupId)
    {
        if (await reportRepository.GetOwnerIdAsync(groupId, Context.ConnectionAborted) is null)
            throw new HubException("Report not found");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(groupId), Context.ConnectionAborted);
    }

    /// <summary> removes the current connection from the group of a report (the chat was closed) </summary>
    public Task RemoveFromGroup(Guid groupId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(groupId), Context.ConnectionAborted);
    }

    public async Task SendMessage(Guid reportId, string content)
    {
        var result = await messageService.AddAsync(reportId, Context.User!.GetUserId(), content);
        if (!result.IsSuccess) throw new HubException(result.ErrorMessage);

        // everyone in the chat (including the sender's own tabs), the client uses SenderId to know which messages are its own
        await Clients.Group(GroupName(reportId)).ReceiveMessage(result.Value!);
    }

    public Task NotifyTyping(Guid reportId)
    {
        return Clients.OthersInGroup(GroupName(reportId)).NotifyTyping(reportId, Context.User!.GetUserId());
    }

    public Task NotifyStoppedTyping(Guid reportId)
    {
        return Clients.OthersInGroup(GroupName(reportId)).NotifyStoppedTyping(reportId, Context.User!.GetUserId());
    }


    // helper methods

    /// <summary> the same text for the same Guid wherever it comes from (a Guid can be written in many ways) </summary>
    private static string GroupName(Guid id) => id.ToString();
}
