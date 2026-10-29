using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Entities;

namespace AabuReports.Core.Abstractions.Repositories;

public interface INotificationRepository
{
    /// <summary>
    /// add the notification to the memory (doesnt save it to DB)
    /// </summary>
    /// <param name="notification"> notification to be added </param>
    void AddNotification(Notification notification);

    /// <summary>
    /// finds a notification by its id, only if it belongs to the given user (entity is tracked, so changes on it are saved by SaveChangesAsync)
    /// </summary>
    /// <returns> the notification, or null if it doesnt exist or belongs to another user </returns>
    Task<Notification?> GetTrackedByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// loads the notifications of one user, newest first (entities are not tracked)
    /// </summary>
    /// <param name="userId"> the user who owns the notifications </param>
    /// <param name="lazyLoadingDto"> skip and take </param>
    /// <param name="unreadOnly"> true to load only the notifications the user didnt see yet </param>
    /// <param name="cancellationToken"></param>
    Task<IReadOnlyList<Notification>> LazyGetByUserIdAsync(Guid userId, LazyLoadingDto lazyLoadingDto, bool unreadOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// counts the notifications of one user that are not read yet
    /// </summary>
    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// marks every unread notification of the user as read (runs directly on the DB, no need to save changes)
    /// </summary>
    /// <returns> how many notifications were changed </returns>
    Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// save all changes in memory to DB
    /// </summary>
    /// <returns> True if changes lines are more than 0 </returns>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}
