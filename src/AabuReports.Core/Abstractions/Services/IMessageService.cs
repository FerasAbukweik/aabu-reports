using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Messages;
using AabuReports.Core.Results;

namespace AabuReports.Core.Abstractions.Services;

/// <summary>
/// the chat of a report: every logged in user can read it and write in it,
/// the owner of the report gets a notification when someone else writes in it
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// saves a new message in the chat of the report, then notifies the owner of the report (unless he is the sender)
    /// </summary>
    /// <param name="reportId"> the report whose chat gets the message </param>
    /// <param name="senderId"> the user who wrote it </param>
    /// <param name="content"> the text (1 to 500 characters after trimming) </param>
    /// <param name="cancellationToken"></param>
    /// <returns> result object with the created message </returns>
    Task<Result<MessageDto>> AddAsync(Guid reportId, Guid senderId, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// gets the messages of the chat of a report, newest first
    /// </summary>
    Task<Result<IReadOnlyList<MessageDto>>> LazyGetByReportIdAsync(Guid reportId, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default);
}
