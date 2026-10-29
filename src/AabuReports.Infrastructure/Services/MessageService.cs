using System.Net;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Messages;
using AabuReports.Core.Entities;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Enums;
using AabuReports.Core.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Services;

public class MessageService(
    IMessageRepository messageRepository,
    IReportRepository reportRepository,
    INotificationService notificationService,
    UserManager<ApplicationUser> userManager,
    ILogger<MessageService> logger) : IMessageService
{
    private const string ReportNotFoundMessage = "Report not found";
    private const string EmptyContentMessage = "The message cannot be empty";
    private const string UserNotFoundMessage = "User not found";

    // the text of the notification sent to the owner of the report (the location title goes in the notification's Subject)
    private const string NewMessageNotificationMessage = "لديك رسالة جديدة على بلاغك";

    public async Task<Result<MessageDto>> AddAsync(Guid reportId, Guid senderId, string content, CancellationToken cancellationToken = default)
    {
        var text = content.Trim();
        if (string.IsNullOrEmpty(text))
            return Result<MessageDto>.Failure(EmptyContentMessage);

        // add it now so we are be able to access the username
        var sender = await userManager.FindByIdAsync(senderId.ToString());
        if (sender is null)
            return Result<MessageDto>.Failure(UserNotFoundMessage, HttpStatusCode.NotFound);

        var message = new Message
        {
            Content = text,
            SentAt = DateTime.UtcNow,
            SenderId = senderId,
            Sender = sender,
            ReportId = reportId
        };

        messageRepository.AddMessage(message);

        if (!await messageRepository.SaveChangesAsync(cancellationToken))
        {
            var errorMessage = "no changes where applied to the DB";
            logger.LogError("{ServiceName}.{MethodName} " + errorMessage,
                nameof(MessageService), nameof(AddAsync));

            return Result<MessageDto>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }

        var ownerResult = await GetOwnerIdAsync(reportId, cancellationToken);
        if (!ownerResult.IsSuccess)
            return ownerResult.MapFailure<MessageDto>();
        
        // tell the owner of the report about the new message, unless he wrote it himself
        if (ownerResult.Value != senderId)
            await NotifyOwnerAsync(ownerResult.Value, reportId, cancellationToken);

        return Result<MessageDto>.Success(message.ToDto(), HttpStatusCode.Created);
    }

    public async Task<Result<IReadOnlyList<MessageDto>>> LazyGetByReportIdAsync(Guid reportId, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default)
    {
        var messages = await messageRepository.LazyGetByReportIdAsync(reportId, lazyLoadingDto, cancellationToken);

        return Result<IReadOnlyList<MessageDto>>.Success(messages.Select(m => m.ToDto()).ToList());
    }


    // private

    /// <summary> checks the report exists </summary>
    /// <returns> the id of the owner of the report on success </returns>
    private async Task<Result<Guid>> GetOwnerIdAsync(Guid reportId, CancellationToken cancellationToken)
    {
        var ownerId = await reportRepository.GetOwnerIdAsync(reportId, cancellationToken);
        if (ownerId is null)
            return Result<Guid>.Failure(ReportNotFoundMessage, HttpStatusCode.NotFound);

        return Result<Guid>.Success(ownerId.Value);
    }

    /// <summary> a notification that fails must not fail the message that is already saved, so it is only logged </summary>
    private async Task NotifyOwnerAsync(Guid ownerId, Guid reportId, CancellationToken cancellationToken)
    {
        // the location title is the subject of the notification
        var report = await reportRepository.GetByIdAsync(reportId, cancellationToken);

        var result = await notificationService.CreateAsync(
            ownerId, NotificationType.NewMessage, NewMessageNotificationMessage, report?.Location?.Title, reportId, cancellationToken);

        if (!result.IsSuccess)
            logger.LogWarning("{ServiceName} could not create a {NotificationType} notification for report {ReportId}: {Error}",
                nameof(MessageService), NotificationType.NewMessage, reportId, result.ErrorMessage);
    }
}
