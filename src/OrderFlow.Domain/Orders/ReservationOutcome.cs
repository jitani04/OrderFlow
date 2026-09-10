namespace OrderFlow.Domain.Orders;

public sealed class ReservationOutcome
{
    private ReservationOutcome(bool isReserved, IReadOnlyList<StockShortage> shortages)
    {
        IsReserved = isReserved;
        Shortages = shortages;
    }

    public bool IsReserved { get; }

    /// <summary>Empty when <see cref="IsReserved"/> is true.</summary>
    public IReadOnlyList<StockShortage> Shortages { get; }

    internal static ReservationOutcome Reserved() => new(true, []);

    internal static ReservationOutcome Short(IReadOnlyList<StockShortage> shortages) => new(false, shortages);

    /// <summary>A sentence fit to return to the caller as the rejection reason.</summary>
    public string DescribeShortages() =>
        Shortages.Count == 0
            ? string.Empty
            : string.Join(
                " ",
                Shortages.Select(shortage =>
                    $"Product {shortage.ProductId} requested {shortage.Requested}, available {shortage.Available}."));
}
