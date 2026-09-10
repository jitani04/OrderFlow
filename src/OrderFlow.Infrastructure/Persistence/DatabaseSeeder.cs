using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderFlow.Domain.Catalog;
using OrderFlow.Domain.Identity;

namespace OrderFlow.Infrastructure.Persistence;

/// <summary>
/// Puts an admin account and a demo catalogue into an empty database.
/// </summary>
public sealed class DatabaseSeeder(
    OrderFlowDbContext dbContext,
    IPasswordHasher passwordHasher,
    IOptions<SeedOptions> options,
    ILogger<DatabaseSeeder> logger)
{
    // Fixed ids rather than random ones, so the seed is stable across restarts and can be
    // referenced directly from the README, the integration tests and curl examples.
    private static readonly (string Id, string Sku, string Name, decimal Price, int OnHand, int Threshold)[] Catalogue =
    [
        ("11111111-1111-1111-1111-111111111111", "OF-KEYB-01", "Mechanical Keyboard", 129.99m, 40, 10),
        ("22222222-2222-2222-2222-222222222222", "OF-MOUS-01", "Wireless Mouse", 49.50m, 60, 15),
        ("33333333-3333-3333-3333-333333333333", "OF-MON-27", "27-inch 4K Monitor", 399.00m, 12, 4),
        ("44444444-4444-4444-4444-444444444444", "OF-DOCK-01", "USB-C Dock", 189.95m, 8, 3),
        ("55555555-5555-5555-5555-555555555555", "OF-CHAIR-01", "Ergonomic Chair", 749.00m, 3, 2),
        ("66666666-6666-6666-6666-666666666666", "OF-CABLE-01", "Thunderbolt Cable", 39.00m, 100, 25),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seed = options.Value;

        await SeedAdminAsync(seed, cancellationToken);

        if (seed.SeedProducts)
        {
            await SeedCatalogueAsync(cancellationToken);
        }
    }

    private async Task SeedAdminAsync(SeedOptions seed, CancellationToken cancellationToken)
    {
        var username = seed.AdminUsername.Trim().ToLowerInvariant();

        if (await dbContext.Users.AnyAsync(user => user.Username == username, cancellationToken))
        {
            return;
        }

        dbContext.Users.Add(new User(
            Guid.NewGuid(),
            username,
            passwordHasher.Hash(seed.AdminPassword),
            Roles.Admin));

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded admin user {Username}.", username);
    }

    private async Task SeedCatalogueAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        var createdAt = DateTimeOffset.UtcNow;

        foreach (var entry in Catalogue)
        {
            var id = Guid.Parse(entry.Id);

            dbContext.Products.Add(new Product(id, entry.Sku, entry.Name, entry.Price, createdAt));
            dbContext.StockLevels.Add(new StockLevel(id, entry.OnHand, entry.Threshold));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} products with stock.", Catalogue.Length);
    }
}
