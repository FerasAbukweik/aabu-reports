namespace AabuReports.Core.Abstractions.Services;

public interface IRedisService
{
    /// <summary> gets a cached value, null (default) if the key is not in the cache </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary> caches a value, expiration overrides are optional; otherwise uses the "Redis" configuration </summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default,
        TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null);

    /// <summary> removes one key from the cache </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary> removes every key that starts with the prefix </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
