using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Messages;

namespace AabuReports.Infrastructure.Services.Logging;

/// <summary> logs the failed results of <see cref="MessageService"/> </summary>
public class LoggingMessageService(
    [FromKeyedServices("inner")] IMessageService innerService,
    ILogger<MessageService> logger) : IMessageService
{
    private const string Name = nameof(MessageService);

    public Task<Result<MessageDto>> AddAsync(Guid reportId, Guid senderId, string content, CancellationToken cancellationToken = default)
        => innerService.AddAsync(reportId, senderId, content, cancellationToken).LogFailure(logger, Name);

    public Task<Result<IReadOnlyList<MessageDto>>> LazyGetByReportIdAsync(Guid reportId, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default)
        => innerService.LazyGetByReportIdAsync(reportId, lazyLoadingDto, cancellationToken).LogFailure(logger, Name);
}
