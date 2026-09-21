namespace OrderFlow.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminUsername { get; set; } = "admin";

    /// <summary>
    /// Hashed at seed time, never stored as written. The default is a development
    /// convenience; every deployed environment overrides it from a secret.
    /// </summary>
    public string AdminPassword { get; set; } = "admin123";

    public string CustomerUsername { get; set; } = "customer";

    /// <summary>
    /// A demo shopper, so the storefront can be tried without registering. Hashed at seed
    /// time like the admin password, and overridden from a secret in any real environment.
    /// </summary>
    public string CustomerPassword { get; set; } = "customer123";

    /// <summary>Whether to put the demo catalogue on the shelves on an empty database.</summary>
    public bool SeedProducts { get; set; } = true;
}
