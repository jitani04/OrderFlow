using OrderFlow.Domain.Orders;

namespace OrderFlow.Api.Models;

public sealed record PlaceOrderRequest
{
    public required string CustomerName { get; init; }

    public required IReadOnlyList<PlaceOrderItem> Items { get; init; }
}

/// <summary>
/// Deliberately carries no price. The API reads the current price from the catalogue, so a
/// caller cannot choose what it pays.
/// </summary>
public sealed record PlaceOrderItem
{
    public required Guid ProductId { get; init; }

    public required int Quantity { get; init; }
}

public sealed record OrderResponse(
    Guid Id,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    string? RejectionReason,
    IReadOnlyList<OrderItemResponse> Items)
{
    public static OrderResponse From(Order order) => new(
        order.Id,
        order.CustomerName,
        order.Status.ToString(),
        order.TotalAmount,
        order.CreatedAt,
        order.RejectionReason,
        [.. order.Items.Select(item => new OrderItemResponse(
            item.ProductId, item.Quantity, item.UnitPrice, item.LineTotal))]);
}

public sealed record OrderItemResponse(Guid ProductId, int Quantity, decimal UnitPrice, decimal LineTotal);
