using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Catalog;

public class Product
{
    private Product()
    {
        // EF Core materialisation.
        Sku = string.Empty;
        Name = string.Empty;
    }

    public Product(Guid id, string sku, string name, decimal price, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainException("A product requires a SKU.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A product requires a name.");
        }

        if (price < 0)
        {
            throw new DomainException("A product price cannot be negative.");
        }

        Id = id;
        Sku = sku.Trim().ToUpperInvariant();
        Name = name.Trim();
        Price = price;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Sku { get; private set; }

    public string Name { get; private set; }

    public decimal Price { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// The product's stock row. One-to-one: a product always has exactly one, created
    /// alongside it.
    /// </summary>
    public StockLevel? Stock { get; private set; }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A product requires a name.");
        }

        Name = name.Trim();
    }

    public void Reprice(decimal price)
    {
        if (price < 0)
        {
            throw new DomainException("A product price cannot be negative.");
        }

        Price = price;
    }
}
