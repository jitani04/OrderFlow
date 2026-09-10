using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Catalog;

namespace OrderFlow.Infrastructure.Persistence;

public sealed class ProductRepository(OrderFlowDbContext dbContext) : IProductRepository
{
    public async Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Products
            .Include(product => product.Stock)
            .AsNoTracking()
            .OrderBy(product => product.Sku)
            .ToListAsync(cancellationToken);

    public Task<Product?> GetAsync(Guid productId, CancellationToken cancellationToken) =>
        dbContext.Products
            .Include(product => product.Stock)
            .FirstOrDefaultAsync(product => product.Id == productId, cancellationToken);

    public Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken) =>
        dbContext.Products.AnyAsync(product => product.Sku == sku.Trim().ToUpper(), cancellationToken);

    public void Add(Product product, StockLevel stock)
    {
        dbContext.Products.Add(product);
        dbContext.StockLevels.Add(stock);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
