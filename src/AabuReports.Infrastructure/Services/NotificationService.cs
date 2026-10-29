using System.Net;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Notifications;
using AabuReports.Core.Entities;
using AabuReports.Core.Enums;
using AabuReports.Core.Results;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Services;

public class NotificationService(
    INotificationRepository notificationRepository,
    INotificationPusher notificationPusher,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task<Result<NotificationDto>> CreateAsync(
        Guid userId,
        NotificationType type,
        string message,
        string? subject = null,
        Guid? reportId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            Type = type,
            Message = message,
            Subject = subject,
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            ReportId = reportId
        };

        notificationRepository.AddNotification(notification);

        if (!await notificationRepository.SaveChangesAsync(cancellationToken))
        {
            var errorMessage = "no changes where applied to the DB";
            logger.LogError("{ServiceName}.{MethodName} " + errorMessage,
                nameof(NotificationService), nameof(CreateAsync));

            return Result<NotificationDto>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }

        var dto = notification.ToDto();

        // the notification is already saved, so the user will see it in the list even if the real time push fails
        try
        {
            await notificationPusher.PushAsync(userId, dto, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "{ServiceName}.{MethodName} failed to push notification {NotificationId} to user {UserId}",
                nameof(NotificationService), nameof(CreateAsync), notification.Id, userId);
        }

        return Result<NotificationDto>.Success(dto, HttpStatusCode.Created);
    }

    public async Task<Result<IReadOnlyList<NotificationDto>>> LazyGetByUserIdAsync(Guid userId, LazyLoadingDto lazyLoadingDto, bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        var notifications = await notificationRepository.LazyGetByUserIdAsync(userId, lazyLoadingDto, unreadOnly, cancellationToken);

        return Result<IReadOnlyList<NotificationDto>>.Success(notifications.Select(n => n.ToDto()).ToList());
    }

    public async Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var count = await notificationRepository.CountUnreadAsync(userId, cancellationToken);

        return Result<int>.Success(count);
    }

    public async Task<Result> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await notificationRepository.MarkAllAsReadAsync(userId, cancellationToken);

        return Result.Success(HttpStatusCode.NoContent);
    }
}
