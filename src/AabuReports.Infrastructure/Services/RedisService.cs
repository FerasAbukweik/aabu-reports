using System.Text.Json;
using AabuReports.Core.Abstractions.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace AabuReports.Infrastructure.Services;

public class RedisService(
    IDistributedCache cache,
    IConnectionMultiplexer redisMultiplexer,
    IConfiguration configuration) : IRedisService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var data = await cache.GetAsync(key, cancellationToken);

        if (data == null)
            return default;

        return JsonSerializer.Deserialize<T>(data, JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions()
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(configuration.GetValue("Redis:AbsoluteExpiration", 60)),
            SlidingExpiration = TimeSpan.FromMinutes(configuration.GetValue("Redis:SlidingExpiration", 10))
        };

        var serializedData = JsonSerializer.Serialize(value, JsonOptions);

        await cache.SetStringAsync(key, serializedData, options, cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(key, cancellationToken);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var instanceName = configuration.GetValue<string>("Redis:InstanceName") ?? string.Empty;
        var searchPattern = $"{instanceName}{prefix}*";

        var db = redisMultiplexer.GetDatabase();
        var endpoints = redisMultiplexer.GetEndPoints();

        foreach (var endpoint in endpoints)
        {
            var server = redisMultiplexer.GetServer(endpoint);

            // replicas are read only, the keys are deleted through the primary
            if (server.IsReplica) continue;

            await foreach (var key in server.KeysAsync(pattern: searchPattern).WithCancellation(cancellationToken))
            {
                await db.KeyDeleteAsync(key);
            }
        }
    }
}
