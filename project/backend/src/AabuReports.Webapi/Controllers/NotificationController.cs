using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Notifications;
using AabuReports.Webapi.ExtensionMethods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AabuReports.Webapi.Controllers;

/// <summary> every action works on the notifications of the logged in user only </summary>
[Authorize]
public class NotificationController(INotificationService notificationService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetMine(
        [FromQuery] LazyLoadingDto lazyLoadingDto,
        [FromQuery] bool unreadOnly,
        CancellationToken cancellationToken)
    {
        var result = await notificationService.LazyGetByUserIdAsync(User.GetUserId(), lazyLoadingDto, unreadOnly, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var result = await notificationService.GetUnreadCountAsync(User.GetUserId(), cancellationToken);

        return result.ToActionResult();
    }
    

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var result = await notificationService.MarkAllAsReadAsync(User.GetUserId(), cancellationToken);

        return result.ToActionResult();
    }
}
