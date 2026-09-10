using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Catalog;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

public sealed class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> builder)
    {
        builder.ToTable("stock_levels");

        // The product id is the key: one stock row per product, not an independent entity.
        builder.HasKey(stock => stock.ProductId);

        builder.Property(stock => stock.QuantityOnHand).IsRequired();
        builder.Property(stock => stock.LowStockThreshold).IsRequired();

        // Derived from the two stored columns.
        builder.Ignore(stock => stock.IsLow);
    }
}
