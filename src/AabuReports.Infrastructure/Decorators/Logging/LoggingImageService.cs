using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Images;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Logging;
using AabuReports.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Decorators.Logging;

/// <summary> logs the failed results of <see cref="ImageService"/> </summary>
public class LoggingImageService(
    IImageService innerService,
    ILogger<ImageService> logger) : IImageService
{
    private const string Name = nameof(ImageService);

    public Task<Result<IReadOnlyList<ImageDto>>> UploadForReportAsync(Guid reportId, IReadOnlyList<IFormFile> images, CancellationToken cancellationToken = default)
        => innerService.UploadForReportAsync(reportId, images, cancellationToken).LogFailure(logger, Name);

    public Task<Result> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => innerService.DeleteByIdAsync(id, cancellationToken).LogFailure(logger, Name);

    public Task<Result> DeleteOrphanedAsync(CancellationToken cancellationToken = default)
        => innerService.DeleteOrphanedAsync(cancellationToken).LogFailure(logger, Name);
}
