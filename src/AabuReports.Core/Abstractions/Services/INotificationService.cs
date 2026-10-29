using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Notifications;
using AabuReports.Core.Enums;
using AabuReports.Core.Results;

namespace AabuReports.Core.Abstractions.Services;

public interface INotificationService
{
    /// <summary>
    /// creates a notification, saves it to the DB, then pushes it to the user in real time (a failed push is only logged, the notification stays saved)
    /// </summary>
    /// <param name="userId"> the user who gets the notification </param>
    /// <param name="type"> what the notification is about </param>
    /// <param name="message"> the text of the notification </param>
    /// <param name="subject"> the thing it is about, e.g. the location title </param>
    /// <param name="reportId"> the report it is about, if any </param>
    /// <param name="cancellationToken"></param>
    /// <returns> result object with the created notification </returns>
    Task<Result<NotificationDto>> CreateAsync(
        Guid userId,
        NotificationType type,
        string message,
        string? subject = null,
        Guid? reportId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// gets the notifications of one user, newest first
    /// </summary>
    Task<Result<IReadOnlyList<NotificationDto>>> LazyGetByUserIdAsync(Guid userId, LazyLoadingDto lazyLoadingDto, bool unreadOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// gets how many notifications the user didnt see yet
    /// </summary>
    Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// marks every notification of the user as read
    /// </summary>
    Task<Result> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
