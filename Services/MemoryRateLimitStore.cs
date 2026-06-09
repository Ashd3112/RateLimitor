using Microsoft.Extensions.Caching.Memory;
using RateLimiterApi.Services;

namespace RateLimiterApi.Services;

public class MemoryRateLimitStore : IRateLimitStore
{
    private readonly IMemoryCache _cache;
    private static readonly object _lock = new();

    public MemoryRateLimitStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<(int Count, DateTime Expiration)> GetOrIncrementAsync(string key, int windowSeconds)
    {
        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out RateLimitRecord? record) || record == null)
            {
                var expiration = DateTime.UtcNow.AddSeconds(windowSeconds);
                record = new RateLimitRecord
                {
                    Count = 1,
                    Expiration = expiration
                };

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(expiration);

                _cache.Set(key, record, cacheEntryOptions);
                return Task.FromResult((record.Count, record.Expiration));
            }
            else
            {
                var timeLeft = record.Expiration - DateTime.UtcNow;

                if (timeLeft <= TimeSpan.Zero)
                {
                    // Window expired but entry is not yet removed by eviction
                    var expiration = DateTime.UtcNow.AddSeconds(windowSeconds);
                    record.Count = 1;
                    record.Expiration = expiration;

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(expiration);

                    _cache.Set(key, record, cacheEntryOptions);
                }
                else
                {
                    record.Count++;
                }

                return Task.FromResult((record.Count, record.Expiration));
            }
        }
    }

    private class RateLimitRecord
    {
        public int Count { get; set; }
        public DateTime Expiration { get; set; }
    }
}
