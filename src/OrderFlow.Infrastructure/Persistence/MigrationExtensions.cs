using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Infrastructure.Persistence;

public static class MigrationExtensions
{
    /// <summary>
    /// Applies pending migrations and seeds, retrying while the database is still coming up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Under docker-compose and Kubernetes the API frequently wins the race against its own
    /// database. Compose health checks and readiness probes narrow that window but do not
    /// close it, so the first connection is retried rather than allowed to crash the pod.
    /// </para>
    /// <para>
    /// Migrating from application startup is a deliberate convenience for a project of this
    /// size: it keeps <c>docker compose up</c> to a single command. A larger deployment
    /// would run migrations as a separate job so that N replicas do not race to apply the
    /// same schema.
    /// </para>
    /// </remarks>
    public static async Task MigrateAndSeedAsync(
        this IServiceProvider services,
        int maxAttempts = 12,
        TimeSpan? delayBetweenAttempts = null,
        CancellationToken cancellationToken = default)
    {
        var delay = delayBetweenAttempts ?? TimeSpan.FromSeconds(5);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await RunAsync(services, cancellationToken);

                logger.LogInformation("Database schema is up to date.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    "Database not ready (attempt {Attempt}/{MaxAttempts}): {Reason}. Retrying in {Delay}s.",
                    attempt, maxAttempts, ex.Message, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }

        // Final attempt outside the catch, so a genuine schema fault surfaces as a crash
        // rather than being retried for ever.
        await RunAsync(services, cancellationToken);
    }

    private static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider
            .GetRequiredService<OrderFlowDbContext>()
            .Database
            .MigrateAsync(cancellationToken);

        await scope.ServiceProvider
            .GetRequiredService<DatabaseSeeder>()
            .SeedAsync(cancellationToken);
    }
}
