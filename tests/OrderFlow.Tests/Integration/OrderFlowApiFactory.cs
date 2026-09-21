using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderFlow.Tests.Integration;

/// <summary>
/// Hosts the real API in-process against a real PostgreSQL in a container.
/// </summary>
/// <remarks>
/// A container rather than an in-memory provider on purpose. The behaviour worth testing
/// here — <c>FOR UPDATE</c> row locks, transaction isolation, foreign keys, unique indexes
/// — either does not exist in the in-memory provider or behaves differently. A test that
/// cannot fail the way production fails is not testing much.
/// </remarks>
public sealed class OrderFlowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same major version as production. Testing against a different engine version than
    // you deploy defeats much of the point of using a real database here.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("orderflow_test")
        .WithUsername("orderflow")
        .WithPassword("orderflow")
        .Build();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Overrides appsettings.json. The API migrates and seeds itself at startup, so the
        // schema and the admin account exist by the time the first request is served.
        builder.UseSetting("ConnectionStrings:OrderFlowDb", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-that-is-long-enough");
        // Generous by default so the rest of the suite — which registers a customer per
        // test — is never throttled. The rate-limit tests lower these on their own host.
        builder.UseSetting("RateLimiting:LoginPermitLimit", "100000");
        builder.UseSetting("RateLimiting:RegisterPermitLimit", "100000");

        builder.UseSetting("Seed:AdminUsername", "admin");
        builder.UseSetting("Seed:AdminPassword", "admin123");
        builder.UseEnvironment("Testing");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

/// <summary>
/// Shares one container and one host across every integration test class. Starting
/// PostgreSQL per class would dominate the runtime.
/// </summary>
[CollectionDefinition(IntegrationCollection.Name)]
public sealed class IntegrationCollection : ICollectionFixture<OrderFlowApiFactory>
{
    public const string Name = "integration";
}
