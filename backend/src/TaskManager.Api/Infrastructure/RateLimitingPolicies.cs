namespace TaskManager.Api.Infrastructure;

internal static class RateLimitingPolicies
{
    /// <summary>Throttles login/register per client IP to slow down credential stuffing.</summary>
    public const string Auth = "auth";
}

internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;
}
