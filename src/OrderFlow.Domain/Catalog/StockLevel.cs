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
        ConcurrencyStamp = Guid.NewGuid();
    }

    public Guid ProductId { get; private set; }

    public int QuantityOnHand { get; private set; }

    public int LowStockThreshold { get; private set; }

    /// <summary>
    /// Changes on every mutation, and is what a caller must echo back to change this row.
    /// </summary>
    /// <remarks>
    /// Stock is written from two directions — orders deduct it, admins correct it — and
    /// the admin write is an absolute value rather than a delta. Without this, an admin who
    /// read "100 on hand", saw an order deduct five, and then submitted 100 would silently
    /// erase that deduction. Two admins editing at once lose each other's writes the same
    /// way. Carrying the stamp turns both of those from silent corruption into a 409.
    /// </remarks>
    public Guid ConcurrencyStamp { get; private set; }

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
        ConcurrencyStamp = Guid.NewGuid();
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
        ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>
    /// True when <paramref name="stamp"/> reflects the version the caller last saw.
    /// </summary>
    public bool MatchesStamp(Guid stamp) => ConcurrencyStamp == stamp;
}
