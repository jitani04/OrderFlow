namespace OrderFlow.Domain.Orders;

/// <summary>
/// Places an order and resolves it against stock in a single database transaction.
/// </summary>
/// <remarks>
/// Declared in the domain and implemented in Infrastructure. The controller depends on this
/// interface rather than on a DbContext, which keeps transaction handling out of the API
/// layer and lets the rule be described in terms the domain owns.
/// </remarks>
public interface IOrderPlacementService
{
    /// <param name="customerId">
    /// The account placing the order, or null when no account owns it. Ownership is what
    /// lets a customer see their own orders and nobody else's.
    /// </param>
    /// <param name="customerName">Captured on the order as it stands now.</param>
    /// <param name="lines">What is being ordered.</param>
    /// <param name="cancellationToken">Cancels the request if the caller disconnects.</param>
    Task<Order> PlaceAsync(
        Guid? customerId,
        string customerName,
        IReadOnlyCollection<OrderLineRequest> lines,
        CancellationToken cancellationToken);
}
