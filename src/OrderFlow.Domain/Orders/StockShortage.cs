namespace OrderFlow.Domain.Orders;

/// <summary>Why one line could not be filled. Available is 0 for an unknown product.</summary>
public sealed record StockShortage(Guid ProductId, int Requested, int Available);
