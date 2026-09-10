using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Catalog;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Sku)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(product => product.Name)
            .HasMaxLength(200)
            .IsRequired();

        // Without an explicit precision Npgsql maps decimal to unbounded numeric and EF
        // warns about it. Money is 18,2 throughout.
        builder.Property(product => product.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(product => product.CreatedAt).IsRequired();

        // A SKU identifies a product to a human, so the database enforces uniqueness
        // rather than trusting every write path to remember to check.
        builder.HasIndex(product => product.Sku).IsUnique();

        // One-to-one. The stock row is keyed by product id, so it cannot exist without
        // its product and there can never be two of them.
        builder.HasOne(product => product.Stock)
            .WithOne()
            .HasForeignKey<StockLevel>(stock => stock.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
