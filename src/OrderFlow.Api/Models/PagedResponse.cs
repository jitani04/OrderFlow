using OrderFlow.Domain.Common;

namespace OrderFlow.Api.Models;

/// <summary>
/// The wire shape of a page. Metadata travels in the body rather than in headers so that a
/// browser client can read it without any CORS header allow-listing.
/// </summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage)
{
    public static PagedResponse<TOut> From<TIn, TOut>(PagedResult<TIn> result, Func<TIn, TOut> map) =>
        new(
            [.. result.Items.Select(map)],
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasPreviousPage,
            result.HasNextPage);
}
