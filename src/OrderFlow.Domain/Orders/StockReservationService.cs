using OrderFlow.Domain.Catalog;
using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Orders;

/// <summary>
/// Decides whether an order can be filled from stock, and applies it if so.
/// </summary>
public static class StockReservationService
{
    /// <summary>
    /// Fills every line of the order or none of them, and resolves the order to Confirmed
    /// or Rejected accordingly.
    /// </summary>
    /// <param name="order">A Pending order. Resolved by this call.</param>
    /// <param name="stockByProduct">
    /// Stock rows for the products the order names, already loaded and locked by the
    /// caller. A missing entry counts as zero available rather than an error, so an order
    /// naming an unknown product is rejected like any other shortage.
    /// </param>
    /// <remarks>
    /// <para>
    /// The two phases are the point. Every line is checked before any line is deducted, so
    /// a partly filled order is unreachable rather than something the caller has to undo.
    /// Deducting as it went and compensating on failure would leave stock missing for an
    /// order that was about to be rejected, if the compensation itself failed.
    /// </para>
    /// <para>
    /// This is pure: it reads and mutates only the objects handed to it, touching no
    /// database and no clock. Persisting the result — and holding the transaction and row
    /// locks that make it safe against concurrent orders — is the caller's job.
    /// </para>
    /// </remarks>
    public static ReservationOutcome Reserve(Order order, IReadOnlyDictionary<Guid, StockLevel> stockByProduct)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(stockByProduct);

        if (order.Status != OrderStatus.Pending)
        {
            throw new DomainException($"Order {order.Id} is already {order.Status} and cannot be reserved again.");
        }

        // Phase 1 — decide. Nothing is mutated here.
        var shortages = new List<StockShortage>();

        foreach (var item in order.Items)
        {
            if (!stockByProduct.TryGetValue(item.ProductId, out var stock))
            {
                shortages.Add(new StockShortage(item.ProductId, item.Quantity, 0));
                continue;
            }

            if (!stock.CanFulfil(item.Quantity))
            {
                shortages.Add(new StockShortage(item.ProductId, item.Quantity, stock.QuantityOnHand));
            }
        }

        if (shortages.Count > 0)
        {
            var outcome = ReservationOutcome.Short(shortages);
            order.Reject(outcome.DescribeShortages());
            return outcome;
        }

        // Phase 2 — apply. Every line is already known to fit, so no call here can fail.
        foreach (var item in order.Items)
        {
            stockByProduct[item.ProductId].Deduct(item.Quantity);
        }

        order.Confirm();
        return ReservationOutcome.Reserved();
    }
}
