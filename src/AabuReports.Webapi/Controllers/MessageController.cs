using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Messages;
using AabuReports.Webapi.ExtensionMethods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AabuReports.Webapi.Controllers;

/// <summary> the old messages of a report's chat (new ones arrive live through the MessageHub) </summary>
[Authorize]
public class MessageController(IMessageService messageService) : ApiControllerBase
{
    /// <summary> one page of the chat, newest first. Every logged in user can read the chat of any report. </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> GetByReport(
        [FromQuery] Guid reportId,
        [FromQuery] LazyLoadingDto lazyLoadingDto,
        CancellationToken cancellationToken)
    {
        var result = await messageService.LazyGetByReportIdAsync(reportId, lazyLoadingDto, cancellationToken);

        return result.ToActionResult();
    }
}
