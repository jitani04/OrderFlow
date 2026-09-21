using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Orders;

/// <summary>
/// The aggregate root. Every state change goes through a method here, so the legal
/// transitions live in one place rather than being re-derived by each caller.
/// </summary>
public class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
        // EF Core materialisation.
        CustomerName = string.Empty;
    }

    private Order(Guid? customerId, string customerName, DateTimeOffset placedAt)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        CustomerName = customerName;
        Status = OrderStatus.Pending;
        CreatedAt = placedAt;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// The account that placed this order, if any.
    /// </summary>
    /// <remarks>
    /// Nullable rather than required. Orders placed before customer accounts existed have
    /// none, and an admin may record an order on someone's behalf. A null here simply means
    /// no customer can claim it, which is the safe default for an ownership check.
    /// </remarks>
    public Guid? CustomerId { get; private set; }

    /// <summary>
    /// The name as it stood when the order was placed, captured rather than looked up, so
    /// renaming an account does not rewrite order history.
    /// </summary>
    public string CustomerName { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Set when the order is rejected; explains which lines could not be filled.</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Read-only so items can only be added through <see cref="Place"/>, which enforces the
    /// invariants. EF Core writes to the <c>_items</c> backing field directly.
    /// </summary>
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public decimal TotalAmount => _items.Sum(item => item.LineTotal);

    /// <summary>
    /// Whether <paramref name="userId"/> placed this order.
    /// </summary>
    /// <remarks>
    /// An unowned order belongs to nobody, so this is false for every caller rather than
    /// true for all of them — the difference between an order no customer can see and one
    /// every customer can.
    /// </remarks>
    public bool BelongsTo(Guid userId) => CustomerId == userId;

    /// <summary>
    /// Creates a Pending order. The only way to construct one, so an order can never exist
    /// without a customer or without at least one line.
    /// </summary>
    public static Order Place(
        Guid? customerId,
        string customerName,
        IReadOnlyCollection<NewOrderLine> lines,
        DateTimeOffset placedAt)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new DomainException("An order requires a customer name.");
        }

        if (lines is null || lines.Count == 0)
        {
            throw new DomainException("An order requires at least one line.");
        }

        if (lines.Select(line => line.ProductId).Distinct().Count() != lines.Count)
        {
            throw new DomainException("An order cannot list the same product on more than one line.");
        }

        var order = new Order(customerId, customerName.Trim(), placedAt);

        foreach (var line in lines)
        {
            order._items.Add(new OrderItem(line.ProductId, line.Quantity, line.UnitPrice));
        }

        return order;
    }

    /// <summary>Marks the order filled. Called only after stock has actually been deducted.</summary>
    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
        {
            throw new DomainException($"Order {Id} is already {Status} and cannot be confirmed.");
        }

        Status = OrderStatus.Confirmed;
        RejectionReason = null;
    }

    /// <summary>Marks the order unfillable. No stock was taken, so nothing needs unwinding.</summary>
    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A rejection requires a reason.");
        }

        if (Status != OrderStatus.Pending)
        {
            throw new DomainException($"Order {Id} is already {Status} and cannot be rejected.");
        }

        Status = OrderStatus.Rejected;
        RejectionReason = reason;
    }
}
