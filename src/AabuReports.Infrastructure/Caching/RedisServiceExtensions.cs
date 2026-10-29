using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Results;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Caching;

/// <summary>
/// the cache-aside steps every cached service repeats (try Redis, miss -> inner service, save the success).
/// Redis is only an optimization: if it is down the call falls through to the inner service
/// (and a failed invalidation is logged) instead of failing the request.
/// </summary>
internal static class RedisServiceExtensions
{
    /// <summary>
    /// returns the cached value if there is one, otherwise calls the inner service and caches the value of a successful result.
    /// Failed results are never cached.
    /// </summary>
    public static async Task<Result<T>> GetOrSetAsync<T>(
        this IRedisService redis,
        ILogger logger,
        string cacheKey,
        Func<Task<Result<T>>> innerCall,
        CancellationToken cancellationToken)
    {
        try
        {
            var cached = await redis.GetAsync<T>(cacheKey, cancellationToken);
            if (cached is not null)
                return Result<T>.Success(cached);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to read {CacheKey} from Redis, falling back to the database", cacheKey);
        }

        var result = await innerCall();
        if (!result.IsSuccess || result.Value is null)
            return result;

        try
        {
            await redis.SetAsync(cacheKey, result.Value, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to write {CacheKey} to Redis", cacheKey);
        }

        return result;
    }

    /// <summary> clears every cached key under the prefixes (called after a successful write to the DB) </summary>
    public static async Task InvalidateAsync(this IRedisService redis, ILogger logger, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            try
            {
                // not tied to the request token: the DB already changed, so the cache must be cleared even if the client left
                await redis.RemoveByPrefixAsync(prefix);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to clear the Redis keys with the prefix {CachePrefix}", prefix);
            }
        }
    }
}
