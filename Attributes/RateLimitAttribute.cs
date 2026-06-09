namespace RateLimiterApi.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RateLimitAttribute : Attribute
{
    public string PolicyName { get; }

    public RateLimitAttribute(string policyName)
    {
        PolicyName = policyName;
    }
}
