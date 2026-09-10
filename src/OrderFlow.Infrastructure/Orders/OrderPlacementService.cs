using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Orders;

/// <summary>
/// Places an order and resolves it against stock inside one database transaction.
/// </summary>
public sealed class OrderPlacementService(
    OrderFlowDbContext dbContext,
    ILogger<OrderPlacementService> logger) : IOrderPlacementService
{
    public async Task<Order> PlaceAsync(
        string customerName,
        IReadOnlyCollection<OrderLineRequest> lines,
        CancellationToken cancellationToken)
    {
        // The connection is configured with EnableRetryOnFailure, and EF refuses to let a
        // manually opened transaction span a retry it does not control — retrying half a
        // transaction would be worse than failing. Running the whole unit of work through
        // the execution strategy hands EF the retry boundary, so a dropped connection
        // replays the entire transaction from the start rather than part of it.
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var productIds = lines
                .Select(line => line.ProductId)
                .Distinct()
                .ToArray();

            var stockByProduct = await LockStockRowsAsync(productIds, cancellationToken);

            var pricesByProduct = await dbContext.Products
                .Where(product => productIds.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, product => product.Price, cancellationToken);

            // "Out of stock" and "no such product" are different failures and must not be
            // conflated. A product that exists but cannot be filled produces a Rejected
            // order worth recording; a product id that does not exist at all is a bad
            // request. Persisting the latter also violates the order_items foreign key,
            // so it is caught here and surfaced as a 400.
            var unknownProductIds = productIds
                .Where(id => !pricesByProduct.ContainsKey(id))
                .ToArray();

            if (unknownProductIds.Length > 0)
            {
                throw new DomainException(
                    $"No product exists with id {string.Join(", ", unknownProductIds)}.");
            }

            // Price comes from the catalogue, never from the caller.
            var order = Order.Place(
                customerName,
                [.. lines.Select(line => new NewOrderLine(
                    line.ProductId,
                    line.Quantity,
                    pricesByProduct.GetValueOrDefault(line.ProductId, 0m)))],
                DateTimeOffset.UtcNow);

            var outcome = StockReservationService.Reserve(order, stockByProduct);

            // The rejected order is persisted too. An order that could not be filled is a
            // fact worth keeping: it tells an admin what customers tried to buy and could
            // not get, which is exactly the signal that drives restocking.
            dbContext.Orders.Add(order);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (outcome.IsReserved)
            {
                logger.LogInformation(
                    "Order {OrderId} confirmed for {CustomerName}: {LineCount} line(s), total {Total}.",
                    order.Id, order.CustomerName, order.Items.Count, order.TotalAmount);
            }
            else
            {
                logger.LogInformation(
                    "Order {OrderId} rejected for {CustomerName}: {Reason}",
                    order.Id, order.CustomerName, order.RejectionReason);
            }

            return order;
        });
    }

    /// <summary>
    /// Loads the stock rows for the requested products and holds a row lock on each until
    /// the transaction commits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A transaction alone does not stop overselling. Under Postgres's default READ
    /// COMMITTED, two orders for the last three widgets both read "3 on hand", both decide
    /// they fit, and both commit — because neither wrote a row the other had already
    /// touched at read time. FOR UPDATE makes the second reader wait for the first to
    /// commit and then see the true remaining quantity.
    /// </para>
    /// <para>
    /// ORDER BY is not cosmetic. Two orders naming the same two products in opposite
    /// sequence would each hold one row and wait on the other for ever. Taking locks in a
    /// consistent order across every caller makes that deadlock unreachable.
    /// </para>
    /// <para>
    /// Column names are quoted because EF maps properties to PascalCase columns by default
    /// while these tables are named in snake_case.
    /// </para>
    /// </remarks>
    private async Task<Dictionary<Guid, Domain.Catalog.StockLevel>> LockStockRowsAsync(
        Guid[] productIds,
        CancellationToken cancellationToken)
    {
        var stockLevels = await dbContext.StockLevels
            .FromSql($"""
                SELECT * FROM stock_levels
                WHERE "ProductId" = ANY({productIds})
                ORDER BY "ProductId"
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);

        return stockLevels.ToDictionary(stock => stock.ProductId);
    }
}
