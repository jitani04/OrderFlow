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

        // IsConcurrencyToken puts this column in the WHERE clause of every UPDATE EF
        // generates for the row. If another writer changed it since this copy was loaded,
        // the UPDATE matches zero rows and EF raises DbUpdateConcurrencyException rather
        // than overwriting their change. That covers the window between load and save,
        // which a check in the controller cannot.
        builder.Property(stock => stock.ConcurrencyStamp)
            .IsConcurrencyToken()
            .IsRequired();

        // Derived from the two stored columns.
        builder.Ignore(stock => stock.IsLow);
    }
}
