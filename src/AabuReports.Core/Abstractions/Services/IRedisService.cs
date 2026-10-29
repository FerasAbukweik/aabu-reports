namespace AabuReports.Core.Abstractions.Services;

public interface IRedisService
{
    /// <summary> gets a cached value, null (default) if the key is not in the cache </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary> caches a value, the expiration times come from the "Redis" section of the configuration </summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary> removes one key from the cache </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary> removes every key that starts with the prefix </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
