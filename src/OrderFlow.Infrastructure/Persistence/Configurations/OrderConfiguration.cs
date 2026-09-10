using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(order => order.Id);

        builder.Property(order => order.CustomerName)
            .HasMaxLength(200)
            .IsRequired();

        // Stored as text rather than an int. Costs a few bytes and makes the table
        // readable in psql, which is worth a lot when diagnosing a rejected order.
        builder.Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(order => order.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(order => order.CreatedAt).IsRequired();

        builder.HasMany(order => order.Items)
            .WithOne()
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Items is a read-only view over the _items field, so EF must write through the
        // field rather than the property.
        builder.Metadata
            .FindNavigation(nameof(Order.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Derived from the lines; never persisted.
        builder.Ignore(order => order.TotalAmount);

        builder.HasIndex(order => order.Status);
        builder.HasIndex(order => order.CreatedAt).IsDescending();
    }
}
