using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.Infrastructure;
using OrderFlow.Api.Models;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
[Produces("application/json")]
public sealed class OrdersController(
    IOrderPlacementService placementService,
    IOrderRepository orders) : ControllerBase
{
    /// <summary>
    /// Places an order. Stock is checked and deducted in a single transaction, so the
    /// response already says whether the order was Confirmed or Rejected.
    /// </summary>
    /// <param name="request">The lines to order, and optionally a customer name.</param>
    /// <param name="cancellationToken">Cancels the request if the caller disconnects.</param>
    /// <remarks>
    /// A rejected order is still created and returned with 201, because the order was
    /// recorded successfully — the request did not fail. The outcome is in the body, where
    /// <c>rejectionReason</c> names the products that were short and by how much.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderResponse>> Place(
        PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = CurrentUser.IdOf(User);
        var username = CurrentUser.UsernameOf(User) ?? "unknown";

        // Only an administrator may name someone else. For a customer the name is taken
        // from their own account, so an order cannot be placed under another person's name.
        var customerName = CurrentUser.IsAdmin(User) && !string.IsNullOrWhiteSpace(request.CustomerName)
            ? request.CustomerName!
            : username;

        var order = await placementService.PlaceAsync(
            customerId,
            customerName,
            [.. request.Items.Select(item => new OrderLineRequest(item.ProductId, item.Quantity))],
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, OrderResponse.From(order));
    }

    /// <summary>
    /// Lists orders, most recent first, one page at a time.
    /// </summary>
    /// <param name="page">1-based. Values below 1 are treated as 1.</param>
    /// <param name="pageSize">Clamped to at most 100.</param>
    /// <param name="status">Optional filter: Pending, Confirmed or Rejected.</param>
    /// <param name="cancellationToken">Cancels the request if the caller disconnects.</param>
    /// <remarks>
    /// An administrator sees every order; a customer sees only their own. The restriction
    /// is decided here from the token, never from a query parameter — a caller-supplied
    /// "customerId" filter would be trivially changed to somebody else's.
    /// <para>
    /// Out-of-range paging values are clamped rather than rejected. A page number past the
    /// end is a harmless client mistake and an empty page answers it honestly, whereas an
    /// oversized page size must not be allowed to scan the whole table.
    /// </para>
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResponse<OrderResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<OrderResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = OrderQuery.DefaultPageSize,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        OrderStatus? parsedStatus = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var value))
            {
                ModelState.AddModelError(
                    nameof(status),
                    $"Unknown status '{status}'. Expected one of: {string.Join(", ", Enum.GetNames<OrderStatus>())}.");

                return ValidationProblem(ModelState);
            }

            parsedStatus = value;
        }

        var restrictTo = CurrentUser.IsAdmin(User) ? null : CurrentUser.IdOf(User);

        var result = await orders.ListAsync(
            new OrderQuery(page, pageSize, parsedStatus, restrictTo),
            cancellationToken);

        return Ok(PagedResponse<OrderResponse>.From(result, OrderResponse.From));
    }

    /// <summary>Gets one order with its lines and status.</summary>
    /// <param name="id">The order to fetch.</param>
    /// <param name="cancellationToken">Cancels the request if the caller disconnects.</param>
    /// <remarks>
    /// A customer asking for somebody else's order gets 404, not 403. A 403 would confirm
    /// the order exists, which is itself something they should not learn.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(id, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        if (!CurrentUser.IsAdmin(User))
        {
            var callerId = CurrentUser.IdOf(User);

            if (callerId is null || !order.BelongsTo(callerId.Value))
            {
                return NotFound();
            }
        }

        return Ok(OrderResponse.From(order));
    }
}
