namespace RateLimiterApi.Services;

public interface IRateLimitStore
{
    Task<(int Count, DateTime Expiration)> GetOrIncrementAsync(string key, int windowSeconds);
}
