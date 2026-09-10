using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Catalog;

/// <summary>
/// How many units of a product are on the shelf, and the point at which that counts as low.
/// </summary>
public class StockLevel
{
    private StockLevel()
    {
        // EF Core materialisation.
    }

    public StockLevel(Guid productId, int quantityOnHand, int lowStockThreshold)
    {
        if (quantityOnHand < 0)
        {
            throw new DomainException("Quantity on hand cannot be negative.");
        }

        if (lowStockThreshold < 0)
        {
            throw new DomainException("Low stock threshold cannot be negative.");
        }

        ProductId = productId;
        QuantityOnHand = quantityOnHand;
        LowStockThreshold = lowStockThreshold;
    }

    public Guid ProductId { get; private set; }

    public int QuantityOnHand { get; private set; }

    public int LowStockThreshold { get; private set; }

    public bool IsLow => QuantityOnHand <= LowStockThreshold;

    public bool CanFulfil(int quantity) => quantity > 0 && quantity <= QuantityOnHand;

    /// <summary>
    /// Takes stock off the shelf for a confirmed order.
    /// </summary>
    /// <remarks>
    /// Internal on purpose. A single line must never be deducted on its own — an order is
    /// filled completely or not at all, and <see cref="Orders.StockReservationService"/>
    /// owns that decision across every line.
    /// </remarks>
    internal void Deduct(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Deducted quantity must be greater than zero.");
        }

        if (quantity > QuantityOnHand)
        {
            throw new DomainException(
                $"Cannot deduct {quantity} of product {ProductId}; only {QuantityOnHand} on hand.");
        }

        QuantityOnHand -= quantity;
    }

    /// <summary>Replaces the stock record wholesale, as an admin stock count would.</summary>
    public void Set(int quantityOnHand, int lowStockThreshold)
    {
        if (quantityOnHand < 0)
        {
            throw new DomainException("Quantity on hand cannot be negative.");
        }

        if (lowStockThreshold < 0)
        {
            throw new DomainException("Low stock threshold cannot be negative.");
        }

        QuantityOnHand = quantityOnHand;
        LowStockThreshold = lowStockThreshold;
    }
}
