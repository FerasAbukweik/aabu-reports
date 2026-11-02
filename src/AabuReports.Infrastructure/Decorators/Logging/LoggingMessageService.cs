using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Messages;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Logging;
using AabuReports.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Decorators.Logging;

/// <summary> logs the failed results of <see cref="MessageService"/> </summary>
public class LoggingMessageService(
    IMessageService innerService,
    ILogger<MessageService> logger) : IMessageService
{
    private const string Name = nameof(MessageService);

    public Task<Result<MessageDto>> AddAsync(Guid reportId, Guid senderId, string content, CancellationToken cancellationToken = default)
        => innerService.AddAsync(reportId, senderId, content, cancellationToken).LogFailure(logger, Name);

    public Task<Result<IReadOnlyList<MessageDto>>> LazyGetByReportIdAsync(Guid reportId, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default)
        => innerService.LazyGetByReportIdAsync(reportId, lazyLoadingDto, cancellationToken).LogFailure(logger, Name);
}
