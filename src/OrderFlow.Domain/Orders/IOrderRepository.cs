namespace OrderFlow.Domain.Orders;

public interface IOrderRepository
{
    Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken);

    Task<Order?> GetAsync(Guid orderId, CancellationToken cancellationToken);
}
