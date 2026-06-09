namespace RateLimiterApi.Models;

public class RateLimitOptions
{
    public const string Position = "RateLimiting";

    public string StorageType { get; set; } = "Memory"; // "Memory" or "Redis"

    public Dictionary<string, PolicyOptions> Policies { get; set; } = new();

    public class PolicyOptions
    {
        public int PermitLimit { get; set; } = 5;
        public int WindowSeconds { get; set; } = 10;
    }
}
