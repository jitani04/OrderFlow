namespace OrderFlow.Api.Infrastructure;

/// <summary>
/// Limits for the anonymous endpoints, which are the ones worth attacking.
/// </summary>
/// <remarks>
/// Configurable rather than hardcoded so a test host can raise them and a deployment can
/// tighten them without a rebuild.
/// </remarks>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Sign-in attempts allowed per client within <see cref="LoginWindowSeconds"/>.</summary>
    public int LoginPermitLimit { get; set; } = 10;

    public int LoginWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Registration is rarer and more costly to abuse — a flood of accounts — so it gets a
    /// smaller allowance over a longer window.
    /// </summary>
    public int RegisterPermitLimit { get; set; } = 5;

    public int RegisterWindowSeconds { get; set; } = 900;

    /// <summary>
    /// Whether to trust X-Forwarded-For. True behind a reverse proxy that is the only way
    /// in; false when the app is exposed directly, where the header is attacker-controlled.
    /// </summary>
    public bool TrustForwardedHeaders { get; set; } = true;
}
