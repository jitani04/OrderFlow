namespace OrderFlow.Domain.Orders;

/// <summary>
/// Why one line could not be filled. <see cref="Available"/> is 0 for an unknown product.
/// </summary>
/// <param name="Sku">
/// Carried so the rejection reason can name the product a human recognises. Null when the
/// caller did not supply a catalogue, in which case the id is used instead.
/// </param>
public sealed record StockShortage(Guid ProductId, int Requested, int Available, string? Sku = null);
