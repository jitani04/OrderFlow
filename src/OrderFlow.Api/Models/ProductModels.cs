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
    DateTimeOffset CreatedAt)
{
    public static ProductResponse From(Product product) => new(
        product.Id,
        product.Sku,
        product.Name,
        product.Price,
        product.Stock?.QuantityOnHand ?? 0,
        product.Stock?.LowStockThreshold ?? 0,
        product.Stock?.IsLow ?? true,
        product.CreatedAt);
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
public sealed record UpdateStockRequest
{
    public required int QuantityOnHand { get; init; }

    public required int LowStockThreshold { get; init; }
}
