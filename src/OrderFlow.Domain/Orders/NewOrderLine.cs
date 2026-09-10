namespace OrderFlow.Domain.Orders;

/// <summary>A requested line, before it becomes part of an <see cref="Order"/>.</summary>
public sealed record NewOrderLine(Guid ProductId, int Quantity, decimal UnitPrice);
