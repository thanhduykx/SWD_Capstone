using System.Text.Json;
using CPMS.BLL.Abstractions.Cache;
using StackExchange.Redis;

namespace CPMS.DAL.Cache;

public sealed class RedisCacheService(IConnectionMultiplexer redis) : ICacheService
{
    private readonly IDatabase _database = redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        RedisValue value = await _database.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);
        await _database.StringSetAsync(key, json, ttl);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        await _database.KeyDeleteAsync(key);
}
