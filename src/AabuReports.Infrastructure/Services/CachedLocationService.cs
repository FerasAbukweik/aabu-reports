using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Locations;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Services;

/// <summary>
/// Redis cache in front of <see cref="LocationService"/>.
/// A location's status and active reports count come from its reports, and a location's title is shown in the report stats,
/// so every write clears the locations AND the reports cache.
/// </summary>
public class CachedLocationService(
    [FromKeyedServices("inner")] ILocationService innerService,
    IRedisService redisService,
    ILogger<CachedLocationService> logger) : ILocationService
{
    private const string AllKey = $"{CacheKeys.Locations}all";
    private static string ByIdKey(Guid id) => $"{CacheKeys.Locations}id:{id}";

    public Task<Result<IReadOnlyList<LocationDto>>> GetAllAsync(CancellationToken cancellationToken = default)
        => redisService.GetOrSetAsync(logger, AllKey,
            () => innerService.GetAllAsync(cancellationToken), cancellationToken);

    public Task<Result<LocationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => redisService.GetOrSetAsync(logger, ByIdKey(id),
            () => innerService.GetByIdAsync(id, cancellationToken), cancellationToken);

    public async Task<Result<LocationDto>> CreateAsync(LocationCreateDto locationCreateDto, CancellationToken cancellationToken = default)
    {
        var result = await innerService.CreateAsync(locationCreateDto, cancellationToken);
        if (result.IsSuccess) await ClearCacheAsync();
        return result;
    }

    public async Task<Result<LocationDto>> UpdateAsync(LocationUpdateDto locationUpdateDto, CancellationToken cancellationToken = default)
    {
        var result = await innerService.UpdateAsync(locationUpdateDto, cancellationToken);
        if (result.IsSuccess) await ClearCacheAsync();
        return result;
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await innerService.DeleteAsync(id, cancellationToken);
        if (result.IsSuccess) await ClearCacheAsync();
        return result;
    }

    // deleting a location deletes its reports, and renaming it changes the report stats, so the reports cache goes too
    private Task ClearCacheAsync() => redisService.InvalidateAsync(logger, CacheKeys.Locations, CacheKeys.Reports);
}
