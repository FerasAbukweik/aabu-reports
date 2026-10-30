using AabuReports.Core.Abstractions.Services;
using AabuReports.Webapi.ExtensionMethods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AabuReports.Webapi.Hubs.MessagesHub;

/// <summary>
/// the live chat of one report. The client connects with "?reportId={guid}" and joins the group named after the report,
/// every logged in user can join the chat of any existing report.
/// </summary>
[Authorize]
public class MessageHub(IMessageService messageService) : Hub<IMessageClient>
{
    public override async Task OnConnectedAsync()
    {
        var reportId = GetReportId();
        if (reportId is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, reportId.Value.ToString());
        await base.OnConnectedAsync();
    }

    public async Task SendMessage(string content)
    {
        var reportId = GetReportId();
        if (reportId is null) throw new HubException("reportId is missing or invalid");

        var result = await messageService.AddAsync(reportId.Value, Context.User!.GetUserId(), content);
        if (!result.IsSuccess) throw new HubException(result.ErrorMessage);

        // everyone in the chat (including the sender's own tabs), the client uses SenderId to know which messages are its own
        await Clients.Group(reportId.Value.ToString()).ReceiveMessage(result.Value!);
    }

    public async Task NotifyTyping()
    {
        var reportId = GetReportId();
        if (reportId is null) return;

        await Clients.OthersInGroup(reportId.Value.ToString()).NotifyTyping(Context.User!.GetUserId());
    }

    public async Task NotifyStoppedTyping()
    {
        var reportId = GetReportId();
        if (reportId is null) return;

        await Clients.OthersInGroup(reportId.Value.ToString()).NotifyStoppedTyping(Context.User!.GetUserId());
    }


    // helper methods

    private Guid? GetReportId()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext == null) return null;

        if (!httpContext.Request.Query.TryGetValue("reportId", out var idString)) return null;

        if (!Guid.TryParse(idString, out var id)) return null;

        return id;
    }
}
