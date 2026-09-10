using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderFlow.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build a context without booting the API host. Only ever used by
/// the design-time tooling; the running application gets its context from DI.
/// </summary>
public sealed class OrderFlowDbContextFactory : IDesignTimeDbContextFactory<OrderFlowDbContext>
{
    public OrderFlowDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ORDERFLOW_DB")
            ?? "Host=localhost;Port=5433;Database=orderflow;Username=orderflow;Password=orderflow";

        var options = new DbContextOptionsBuilder<OrderFlowDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new OrderFlowDbContext(options);
    }
}
