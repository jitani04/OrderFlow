using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Orders;

public class OrderItem
{
    private OrderItem()
    {
        // EF Core materialisation.
    }

    internal OrderItem(Guid productId, int quantity, decimal unitPrice)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Order item quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("Order item unit price cannot be negative.");
        }

        Id = Guid.NewGuid();
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>
    /// The price captured when the order was placed, not a live lookup. Repricing a
    /// product later must not silently change what a customer was charged.
    /// </summary>
    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
