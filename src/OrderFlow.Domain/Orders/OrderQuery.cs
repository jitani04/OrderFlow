namespace OrderFlow.Domain.Orders;

/// <summary>
/// What to fetch from the order history.
/// </summary>
/// <remarks>
/// The bounds are enforced here rather than trusted from the caller, so no route into the
/// repository can ask for an unbounded page.
/// </remarks>
public sealed record OrderQuery
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public OrderQuery(
        int page = 1,
        int pageSize = DefaultPageSize,
        OrderStatus? status = null,
        Guid? customerId = null)
    {
        Page = page < 1 ? 1 : page;
        PageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        Status = status;
        CustomerId = customerId;
    }

    public int Page { get; }

    public int PageSize { get; }

    /// <summary>Null means every status.</summary>
    public OrderStatus? Status { get; }

    /// <summary>
    /// Restricts results to one account's orders. Null means no restriction, which only an
    /// administrator may ask for — the controller decides, never the caller.
    /// </summary>
    public Guid? CustomerId { get; }

    public int Skip => (Page - 1) * PageSize;
}
