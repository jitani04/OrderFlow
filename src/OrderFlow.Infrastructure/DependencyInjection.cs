using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Domain.Catalog;
using OrderFlow.Domain.Identity;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Identity;
using OrderFlow.Infrastructure.Orders;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderFlowInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        services.AddDbContext<OrderFlowDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("OrderFlowDb"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations_history");

                    // Transient faults — a failover, a dropped connection, a restarting
                    // database — resolve on their own. This is also why order placement
                    // runs through the execution strategy: EF will not let a manual
                    // transaction span a retry unless it owns the retry boundary.
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                }));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrderPlacementService, OrderPlacementService>();
        services.AddScoped<DatabaseSeeder>();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        // Reports the service unhealthy if it cannot reach its database, which is what the
        // readiness probe checks.
        services.AddHealthChecks()
            .AddDbContextCheck<OrderFlowDbContext>("database");

        return services;
    }
}
