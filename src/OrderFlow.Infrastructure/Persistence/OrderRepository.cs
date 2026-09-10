using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Infrastructure.Persistence;

public sealed class OrderRepository(OrderFlowDbContext dbContext) : IOrderRepository
{
    public async Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Orders
            .Include(order => order.Items)
            .AsNoTracking()
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<Order?> GetAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Orders
            .Include(order => order.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
}
