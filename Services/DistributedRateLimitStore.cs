using Microsoft.Extensions.Caching.Distributed;
using RateLimiterApi.Services;
using System.Text.Json;

namespace RateLimiterApi.Services;

public class DistributedRateLimitStore : IRateLimitStore
{
    private readonly IDistributedCache _cache;

    public DistributedRateLimitStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<(int Count, DateTime Expiration)> GetOrIncrementAsync(string key, int windowSeconds)
    {
        var data = await _cache.GetStringAsync(key);
        RateLimitRecord? record = null;

        if (!string.IsNullOrEmpty(data))
        {
            try
            {
                record = JsonSerializer.Deserialize<RateLimitRecord>(data);
            }
            catch
            {
                // Fallback if deserialization fails
            }
        }

        if (record == null)
        {
            var expiration = DateTime.UtcNow.AddSeconds(windowSeconds);
            record = new RateLimitRecord
            {
                Count = 1,
                Expiration = expiration
            };

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expiration
            };

            await _cache.SetStringAsync(key, JsonSerializer.Serialize(record), options);
            return (record.Count, record.Expiration);
        }
        else
        {
            var timeLeft = record.Expiration - DateTime.UtcNow;

            if (timeLeft <= TimeSpan.Zero)
            {
                var expiration = DateTime.UtcNow.AddSeconds(windowSeconds);
                record.Count = 1;
                record.Expiration = expiration;

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = expiration
                };

                await _cache.SetStringAsync(key, JsonSerializer.Serialize(record), options);
            }
            else
            {
                record.Count++;
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = record.Expiration
                };

                await _cache.SetStringAsync(key, JsonSerializer.Serialize(record), options);
            }

            return (record.Count, record.Expiration);
        }
    }

    private class RateLimitRecord
    {
        public int Count { get; set; }
        public DateTime Expiration { get; set; }
    }
}
