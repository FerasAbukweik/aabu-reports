using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Services;

/// <summary>
/// Redis cache in front of <see cref="ReportService"/>.
/// Cached: the two stats cards and the map reports of a location (small, shared and read a lot).
/// Not cached: the filtered table pages and the get-by-id (too many filter combinations, per user, and the owner
/// expects to see a state change at once), those always go to the database.
/// Every write clears the reports cache AND the locations cache (a location's status and count come from its reports).
/// </summary>
public class CachedReportService(
    [FromKeyedServices("inner")] IReportService innerService,
    IRedisService redisService,
    ILogger<CachedReportService> logger) : IReportService
{
    private const string MaintenanceStatsKey = $"{CacheKeys.Reports}stats:maintenance";
    private static string HistoryStatsKey(Guid? userId) => $"{CacheKeys.Reports}stats:history:{(userId?.ToString() ?? "all")}";
    private static string MapKey(Guid locationId, LazyLoadingDto lazy) => $"{CacheKeys.Reports}map:{locationId}:{lazy.Skip}:{lazy.Take}";

    // ---------- cached reads ----------

    public Task<Result<IReadOnlyList<ReportMapDto>>> LazyGetMapReportsAsync(Guid locationId, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default)
        => redisService.GetOrSetAsync(logger, MapKey(locationId, lazyLoadingDto),
            () => innerService.LazyGetMapReportsAsync(locationId, lazyLoadingDto, cancellationToken), cancellationToken);

    public Task<Result<MaintenanceStatsDto>> GetMaintenanceStatsAsync(CancellationToken cancellationToken = default)
        => redisService.GetOrSetAsync(logger, MaintenanceStatsKey,
            () => innerService.GetMaintenanceStatsAsync(cancellationToken), cancellationToken);

    public Task<Result<ReportHistoryStatsDto>> GetHistoryStatsAsync(Guid? userId, CancellationToken cancellationToken = default)
        => redisService.GetOrSetAsync(logger, HistoryStatsKey(userId),
            () => innerService.GetHistoryStatsAsync(userId, cancellationToken), cancellationToken);

    // ---------- reads that always hit the database ----------

    public Task<Result<IReadOnlyList<ReportDto>>> LazyGetFilteredAsync(Guid? userId, ReportFilterDto filterDto, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default)
        => innerService.LazyGetFilteredAsync(userId, filterDto, lazyLoadingDto, cancellationToken);

    public Task<Result<ReportDto>> GetByIdAsync(string idOrShortId, CancellationToken cancellationToken = default)
        => innerService.GetByIdAsync(idOrShortId, cancellationToken);

    // ---------- writes: run the inner service, then clear the cache ----------

    public async Task<Result<ReportCreateResponseDto>> CreateForLocationIdAsync(Guid userId, ReportCreateForLocationDto reportData, CancellationToken cancellationToken = default)
    {
        var result = await innerService.CreateForLocationIdAsync(userId, reportData, cancellationToken);
        if (result.IsSuccess) await ClearCacheAsync();
        return result;
    }

    public async Task<Result<ReportCreateResponseDto>> CreateForCoordinates(Guid userId, ReportCreateForCoordinatesDto reportData, CancellationToken cancellationToken = default)
    {
        var result = await innerService.CreateForCoordinates(userId, reportData, cancellationToken);
        if (result.IsSuccess) await ClearCacheAsync();
        return result;
    }

    public async Task<Result<ReportDto>> UpdateStateAsync(ReportUpdateStateDto reportUpdateStateDto, CancellationToken cancellationToken = default)
    {
        var result = await innerService.UpdateStateAsync(reportUpdateStateDto, cancellationToken);
        if (result.IsSuccess) await ClearCacheAsync();
        return result;
    }

    public async Task<Result<ReportDto>> RateAsync(Guid userId, ReportRateDto reportRateDto, CancellationToken cancellationToken = default)
    {
        var result = await innerService.RateAsync(userId, reportRateDto, cancellationToken);
        if (result.IsSuccess) await ClearCacheAsync();
        return result;
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await innerService.DeleteAsync(id, cancellationToken);
        if (result.IsSuccess) await ClearCacheAsync();
        return result;
    }

    private Task ClearCacheAsync() => redisService.InvalidateAsync(logger, CacheKeys.Reports, CacheKeys.Locations);
}
