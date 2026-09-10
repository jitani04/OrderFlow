using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        return product is null ? NotFound() : Ok(ProductResponse.From(product));
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

    /// <summary>Replaces a product's stock record, as a stock count would.</summary>
    [HttpPut("{id:guid}/stock")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> UpdateStock(
        Guid id,
        UpdateStockRequest request,
        CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(id, cancellationToken);

        if (product?.Stock is null)
        {
            return NotFound();
        }

        product.Stock.Set(request.QuantityOnHand, request.LowStockThreshold);
        await products.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Set stock for {Sku} to {Quantity} (threshold {Threshold}).",
            product.Sku, request.QuantityOnHand, request.LowStockThreshold);

        return Ok(ProductResponse.From(product));
    }
}
