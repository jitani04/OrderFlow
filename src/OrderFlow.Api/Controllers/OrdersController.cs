using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        var order = await placementService.PlaceAsync(
            request.CustomerName,
            [.. request.Items.Select(item => new OrderLineRequest(item.ProductId, item.Quantity))],
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, OrderResponse.From(order));
    }

    /// <summary>Lists orders, most recent first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrderResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> List(CancellationToken cancellationToken)
    {
        var all = await orders.ListAsync(cancellationToken);

        return Ok(all.Select(OrderResponse.From).ToList());
    }

    /// <summary>Gets one order with its lines and status.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(id, cancellationToken);

        return order is null ? NotFound() : Ok(OrderResponse.From(order));
    }
}
