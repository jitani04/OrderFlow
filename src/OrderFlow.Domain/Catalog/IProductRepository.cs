namespace OrderFlow.Domain.Catalog;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken);

    Task<Product?> GetAsync(Guid productId, CancellationToken cancellationToken);

    Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken);

    void Add(Product product, StockLevel stock);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
