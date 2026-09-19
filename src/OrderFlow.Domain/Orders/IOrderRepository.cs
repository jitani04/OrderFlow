using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Orders;

public interface IOrderRepository
{
    /// <summary>
    /// One page of order history, newest first.
    /// </summary>
    /// <remarks>
    /// Paged rather than returning everything. Order history only grows, so an unbounded
    /// list endpoint is a slow-acting outage: it works in development and times out — or
    /// exhausts memory — once the table is large enough.
    /// </remarks>
    Task<PagedResult<Order>> ListAsync(OrderQuery query, CancellationToken cancellationToken);

    Task<Order?> GetAsync(Guid orderId, CancellationToken cancellationToken);
}
