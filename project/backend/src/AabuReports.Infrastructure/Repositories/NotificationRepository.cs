using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AabuReports.Infrastructure.Repositories;

public class NotificationRepository(
    ApplicationDbContext dbContext
    ) : INotificationRepository
{
    public void AddNotification(Notification notification)
    {
        dbContext.Notifications.Add(notification);
    }

    public async Task<Notification?> GetTrackedByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Notifications
            .SingleOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> LazyGetByUserIdAsync(Guid userId, LazyLoadingDto lazyLoadingDto, bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (unreadOnly) query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(lazyLoadingDto.Skip)
            .Take(lazyLoadingDto.Take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken) > 0;
    }
}
