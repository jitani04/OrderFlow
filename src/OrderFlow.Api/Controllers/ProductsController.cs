using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using OrderFlow.Api.Models;
using OrderFlow.Domain.Catalog;
using OrderFlow.Domain.Identity;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
[Produces("application/json")]
public sealed class ProductsController(
    IProductRepository products,
    ILogger<ProductsController> logger) : ControllerBase
{
    /// <summary>Lists every product with its current stock.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> List(CancellationToken cancellationToken)
    {
        var all = await products.ListAsync(cancellationToken);

        return Ok(all.Select(ProductResponse.From).ToList());
    }

    /// <summary>Gets one product with its current stock.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        // The version a caller must echo back in If-Match to change this product's stock.
        SetETag(product.Stock?.ConcurrencyStamp);

        return Ok(ProductResponse.From(product));
    }

    /// <summary>Creates a product together with its opening stock.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        // Checked here for a clean 409; the unique index on SKU is what actually guarantees
        // it, since two concurrent creates could both pass this check.
        if (await products.SkuExistsAsync(request.Sku, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Title = "That SKU already exists.",
                Detail = $"A product with SKU '{request.Sku.Trim().ToUpperInvariant()}' is already in the catalogue.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        var productId = Guid.NewGuid();

        var product = new Product(productId, request.Sku, request.Name, request.Price, DateTimeOffset.UtcNow);
        var stock = new StockLevel(productId, request.QuantityOnHand, request.LowStockThreshold);

        products.Add(product, stock);
        await products.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created product {Sku} ({ProductId}).", product.Sku, product.Id);

        // Re-read so the response carries the stock navigation the caller expects.
        var created = await products.GetAsync(productId, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = productId }, ProductResponse.From(created!));
    }

    /// <summary>
    /// Replaces a product's stock record, as a stock count would.
    /// </summary>
    /// <param name="id">The product to update.</param>
    /// <param name="request">The new absolute quantities.</param>
    /// <param name="cancellationToken">Cancels the request if the caller disconnects.</param>
    /// <remarks>
    /// Requires an <c>If-Match</c> header carrying the version from a prior GET.
    /// <para>
    /// This body is an absolute count, not a delta, so applying it blindly would overwrite
    /// whatever happened since the caller last looked — including stock an order deducted
    /// in between. Requiring the version turns that silent loss into a 409, and the caller
    /// re-reads and decides what it actually meant.
    /// </para>
    /// </remarks>
    [HttpPut("{id:guid}/stock")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<ProductResponse>> UpdateStock(
        Guid id,
        UpdateStockRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(out var expectedVersion))
        {
            // 428, not 400: the request is well formed, it just may not be applied
            // unconditionally. The name says exactly what is missing.
            return StatusCode(StatusCodes.Status428PreconditionRequired, new ProblemDetails
            {
                Title = "If-Match is required.",
                Detail = "Read the product first and send its version in an If-Match header, "
                         + "so this update cannot overwrite a change you have not seen.",
                Status = StatusCodes.Status428PreconditionRequired,
            });
        }

        var product = await products.GetAsync(id, cancellationToken);

        if (product?.Stock is null)
        {
            return NotFound();
        }

        if (!product.Stock.MatchesStamp(expectedVersion))
        {
            return StockConflict(product);
        }

        product.Stock.Set(request.QuantityOnHand, request.LowStockThreshold);

        try
        {
            await products.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The check above covers a caller working from a stale read. This covers the
            // narrower window between that check and this save, where the concurrency
            // token in the UPDATE's WHERE clause matches nothing.
            logger.LogInformation("Concurrent stock update lost the race for product {ProductId}.", id);

            var current = await products.GetAsync(id, cancellationToken);
            return current is null ? NotFound() : StockConflict(current);
        }

        logger.LogInformation(
            "Set stock for {Sku} to {Quantity} (threshold {Threshold}).",
            product.Sku, request.QuantityOnHand, request.LowStockThreshold);

        SetETag(product.Stock.ConcurrencyStamp);

        return Ok(ProductResponse.From(product));
    }

    /// <summary>Parses If-Match, accepting both a quoted ETag and a bare GUID.</summary>
    private bool TryReadIfMatch(out Guid version)
    {
        version = Guid.Empty;

        var header = Request.Headers[HeaderNames.IfMatch].ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        // Wildcard means "any current version", which is the unconditional write this
        // endpoint exists to prevent.
        if (header.Trim() == "*")
        {
            return false;
        }

        return Guid.TryParse(header.Trim().Trim('"').TrimStart('W', '/'), out version);
    }

    private void SetETag(Guid? version)
    {
        if (version is { } value)
        {
            Response.Headers.ETag = $"\"{value}\"";
        }
    }

    private ActionResult StockConflict(Product product) => Conflict(new ProblemDetails
    {
        Title = "The product changed since you read it.",
        Detail = $"Stock for {product.Sku} is now {product.Stock?.QuantityOnHand ?? 0} on hand. "
                 + "Re-read the product and retry with its current version.",
        Status = StatusCodes.Status409Conflict,
    });
}
