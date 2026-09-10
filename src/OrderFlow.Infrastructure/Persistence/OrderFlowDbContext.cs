using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Catalog;
using OrderFlow.Domain.Identity;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Infrastructure.Persistence;

public class OrderFlowDbContext(DbContextOptions<OrderFlowDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<StockLevel> StockLevels => Set<StockLevel>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderFlowDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
