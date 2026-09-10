namespace OrderFlow.Domain.Orders;

public enum OrderStatus
{
    /// <summary>Constructed but not yet resolved against stock. Never persisted in this state.</summary>
    Pending = 0,

    /// <summary>Every line was filled and stock has been deducted.</summary>
    Confirmed = 1,

    /// <summary>At least one line could not be filled; no stock was taken.</summary>
    Rejected = 2,
}
