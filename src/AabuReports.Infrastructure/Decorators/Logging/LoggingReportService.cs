using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Logging;
using AabuReports.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Decorators.Logging;

/// <summary> logs the failed results of <see cref="ReportService"/> </summary>
public class LoggingReportService(
    IReportService innerService,
    ILogger<ReportService> logger) : IReportService
{
    private const string Name = nameof(ReportService);

    public Task<Result<IReadOnlyList<ReportDto>>> LazyGetFilteredAsync(Guid? userId, ReportFilterDto filterDto, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default)
        => innerService.LazyGetFilteredAsync(userId, filterDto, lazyLoadingDto, cancellationToken).LogFailure(logger, Name);

    public Task<Result<ReportDto>> GetByIdAsync(string idOrShortId, CancellationToken cancellationToken = default)
        => innerService.GetByIdAsync(idOrShortId, cancellationToken).LogFailure(logger, Name);

    public Task<Result<ReportCreateResponseDto>> CreateForLocationIdAsync(Guid userId, ReportCreateForLocationDto reportData, CancellationToken cancellationToken = default)
        => innerService.CreateForLocationIdAsync(userId, reportData, cancellationToken).LogFailure(logger, Name);

    public Task<Result<ReportCreateResponseDto>> CreateForCoordinates(Guid userId, ReportCreateForCoordinatesDto reportData, CancellationToken cancellationToken = default)
        => innerService.CreateForCoordinates(userId, reportData, cancellationToken).LogFailure(logger, Name);

    public Task<Result<ReportDto>> UpdateStateAsync(ReportUpdateStateDto reportUpdateStateDto, CancellationToken cancellationToken = default)
        => innerService.UpdateStateAsync(reportUpdateStateDto, cancellationToken).LogFailure(logger, Name);

    public Task<Result<ReportDto>> RateAsync(Guid userId, ReportRateDto reportRateDto, CancellationToken cancellationToken = default)
        => innerService.RateAsync(userId, reportRateDto, cancellationToken).LogFailure(logger, Name);

    public Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => innerService.DeleteAsync(id, cancellationToken).LogFailure(logger, Name);

    public Task<Result<IReadOnlyList<ReportMapDto>>> LazyGetMapReportsAsync(Guid locationId, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default)
        => innerService.LazyGetMapReportsAsync(locationId, lazyLoadingDto, cancellationToken).LogFailure(logger, Name);

    public Task<Result<MaintenanceStatsDto>> GetMaintenanceStatsAsync(CancellationToken cancellationToken = default)
        => innerService.GetMaintenanceStatsAsync(cancellationToken).LogFailure(logger, Name);

    public Task<Result<ReportHistoryStatsDto>> GetHistoryStatsAsync(Guid? userId, CancellationToken cancellationToken = default)
        => innerService.GetHistoryStatsAsync(userId, cancellationToken).LogFailure(logger, Name);
}
