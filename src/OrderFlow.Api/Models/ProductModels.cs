using OrderFlow.Domain.Catalog;

namespace OrderFlow.Api.Models;

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    decimal Price,
    int QuantityOnHand,
    int LowStockThreshold,
    bool IsLow,
    DateTimeOffset CreatedAt,
    Guid Version)
{
    public static ProductResponse From(Product product) => new(
        product.Id,
        product.Sku,
        product.Name,
        product.Price,
        product.Stock?.QuantityOnHand ?? 0,
        product.Stock?.LowStockThreshold ?? 0,
        product.Stock?.IsLow ?? true,
        product.CreatedAt,
        // Also returned as an ETag header. Repeated in the body so a client that does not
        // read headers — the browser fetch default — can still send it back on a write.
        product.Stock?.ConcurrencyStamp ?? Guid.Empty);
}

public sealed record CreateProductRequest
{
    public required string Sku { get; init; }

    public required string Name { get; init; }

    public required decimal Price { get; init; }

    public required int QuantityOnHand { get; init; }

    public required int LowStockThreshold { get; init; }
}

/// <summary>
/// A full replacement of the stock record, which is what PUT means. An absolute count also
/// matches how the value is actually produced: someone counts the shelf and reports the
/// total, rather than computing a delta.
/// </summary>
/// <remarks>
/// Because it is absolute rather than a delta, it must be applied to the version the
/// caller actually saw — hence the required If-Match header on the endpoint.
/// </remarks>
public sealed record UpdateStockRequest
{
    public required int QuantityOnHand { get; init; }

    public required int LowStockThreshold { get; init; }
}
