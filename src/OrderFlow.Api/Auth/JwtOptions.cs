namespace OrderFlow.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "orderflow";

    public string Audience { get; set; } = "orderflow-api";

    /// <summary>
    /// HMAC-SHA256 needs at least 32 bytes. The value in appsettings is a development
    /// placeholder; every deployed environment overrides it from a secret.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;
}
