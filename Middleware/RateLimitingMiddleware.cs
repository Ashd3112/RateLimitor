using Microsoft.Extensions.Options;
using RateLimiterApi.Attributes;
using RateLimiterApi.Models;
using RateLimiterApi.Services;
using System.Net;

namespace RateLimiterApi.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitStore _store;
    private readonly RateLimitOptions _options;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IRateLimitStore store,
        IOptions<RateLimitOptions> options,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Resolve Policy Name
        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            // Bypass rate limiter for endpoints that don't exist (to avoid counting 404s)
            await _next(context);
            return;
        }

        var rateLimitAttribute = endpoint.Metadata.GetMetadata<RateLimitAttribute>();
        var policyName = rateLimitAttribute?.PolicyName ?? "Default";

        // 2. Fetch Policy Config
        if (!_options.Policies.TryGetValue(policyName, out var policy))
        {
            // Fallback policy if config is missing
            policy = new RateLimitOptions.PolicyOptions
            {
                PermitLimit = 5,
                WindowSeconds = 10
            };
            _logger.LogWarning("Rate limiting policy '{PolicyName}' was requested but not found in configurations. Falling back to default settings.", policyName);
        }

        // 3. Resolve Client Key
        var clientKey = GetClientKey(context);
        var cacheKey = $"rate-limit:{policyName}:{clientKey}";

        // 4. Evaluate Limit
        var (count, expiration) = await _store.GetOrIncrementAsync(cacheKey, policy.WindowSeconds);
        var remainingRequests = policy.PermitLimit - count;

        if (count > policy.PermitLimit)
        {
            var secondsLeft = (int)Math.Ceiling((expiration - DateTime.UtcNow).TotalSeconds);
            if (secondsLeft < 0) secondsLeft = 0;

            _logger.LogWarning(
                "Rate limit exceeded for client '{ClientKey}' using policy '{PolicyName}'. Blocked request to {Path}. Remaining retry window: {RetrySeconds}s.",
                clientKey,
                policyName,
                context.Request.Path,
                secondsLeft);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = secondsLeft.ToString();
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                Message = "Rate limit exceeded. Please try again later.",
                PolicyName = policyName,
                RetryAfterSeconds = secondsLeft
            });
            return;
        }

        _logger.LogInformation(
            "Request from client '{ClientKey}' accepted under policy '{PolicyName}'. Remaining requests: {Remaining}/{Limit}.",
            clientKey,
            policyName,
            Math.Max(0, remainingRequests),
            policy.PermitLimit);

        // Add headers for successful requests
        context.Response.Headers["X-RateLimit-Limit"] = policy.PermitLimit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, remainingRequests).ToString();

        await _next(context);
    }

    private static string GetClientKey(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Client-Id", out var clientId))
        {
            return clientId.ToString();
        }

        var ip = context.Connection.RemoteIpAddress;
        return ip != null ? ip.ToString() : "unknown";
    }
}
