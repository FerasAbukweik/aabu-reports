using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Notifications;
using AabuReports.Core.Enums;

namespace AabuReports.Infrastructure.Services.Logging;

/// <summary>
/// logs the failed results of <see cref="NotificationService"/>.
/// callers that must not fail because of a notification (reports, messages) swallow what this rethrows, so the exception is logged here
/// </summary>
public class LoggingNotificationService(
    [FromKeyedServices("inner")] INotificationService innerService,
    ILogger<NotificationService> logger) : INotificationService
{
    private const string Name = nameof(NotificationService);

    public async Task<Result<NotificationDto>> CreateAsync(Guid userId, NotificationType type, string message, string? subject = null, Guid? reportId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // a failed notification is never fatal for the caller, so a 4xx here is only a warning worth seeing
            return await innerService.CreateAsync(userId, type, message, subject, reportId, cancellationToken)
                .LogFailure(logger, Name, logClientErrors: true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "{ServiceName}.{MethodName} failed to create a {NotificationType} notification for user {UserId} (report {ReportId})",
                Name, nameof(CreateAsync), type, userId, reportId);
            throw;
        }
    }

    public Task<Result<IReadOnlyList<NotificationDto>>> LazyGetByUserIdAsync(Guid userId, LazyLoadingDto lazyLoadingDto, bool unreadOnly = false, CancellationToken cancellationToken = default)
        => innerService.LazyGetByUserIdAsync(userId, lazyLoadingDto, unreadOnly, cancellationToken).LogFailure(logger, Name);

    public Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
        => innerService.GetUnreadCountAsync(userId, cancellationToken).LogFailure(logger, Name);

    public Task<Result> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
        => innerService.MarkAllAsReadAsync(userId, cancellationToken).LogFailure(logger, Name);
}
