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
    Task<Order> PlaceAsync(
        string customerName,
        IReadOnlyCollection<OrderLineRequest> lines,
        CancellationToken cancellationToken);
}
