using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Infrastructure.Persistence;

public sealed class OrderRepository(OrderFlowDbContext dbContext) : IOrderRepository
{
    public async Task<PagedResult<Order>> ListAsync(OrderQuery query, CancellationToken cancellationToken)
    {
        var filtered = dbContext.Orders.AsNoTracking();

        if (query.Status is { } status)
        {
            filtered = filtered.Where(order => order.Status == status);
        }

        // Applied here rather than filtered after the fact, so the restriction reaches the
        // database and the total count reflects it too.
        if (query.CustomerId is { } customerId)
        {
            filtered = filtered.Where(order => order.CustomerId == customerId);
        }

        // Counted before paging, and without the Include: the caller needs the total number
        // of matching orders, and joining the lines in would make the database do work whose
        // result is thrown away.
        var totalCount = await filtered.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return PagedResult<Order>.Empty(query.Page, query.PageSize);
        }

        var items = await filtered
            .Include(order => order.Items)
            // ThenBy(Id) makes the ordering total. CreatedAt has microsecond precision, so
            // collisions are rare in practice and paging looks fine without this — but SQL
            // gives no guarantee about the relative order of tied rows, and two queries may
            // order them differently. If that happened between fetching page 1 and page 2,
            // one order would appear twice and another would never be returned. Ties do
            // occur: bulk imports, restored data, or a coarser clock. Cheap insurance
            // against a bug that would be near-impossible to reproduce.
            .OrderByDescending(order => order.CreatedAt)
            .ThenBy(order => order.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Order>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<Order?> GetAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Orders
            .Include(order => order.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
}
