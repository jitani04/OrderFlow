namespace OrderFlow.Domain.Orders;

/// <summary>
/// What a caller asks for: a product and a quantity, and nothing else.
/// </summary>
/// <remarks>
/// Notably not a price. The service reads the current price from the catalogue and records
/// it on the order line, so a caller cannot name its own price, and repricing the product
/// later does not change what an existing order was charged.
/// </remarks>
public sealed record OrderLineRequest(Guid ProductId, int Quantity);
