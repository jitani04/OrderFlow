using FluentValidation;
using OrderFlow.Api.Models;

namespace OrderFlow.Api.Validation;

/// <summary>
/// These validators exist for the error message, not for the guarantee. The domain enforces
/// the same invariants again and is the thing that actually protects the data; this layer
/// turns a bad request into a 400 with a field-level explanation instead of a 500.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Username).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(request => request.Sku)
            .NotEmpty().WithMessage("SKU is required.")
            .MaximumLength(50)
            .Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("SKU may contain only letters, digits, dots, dashes and underscores.");

        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(request => request.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price cannot be negative.")
            .LessThanOrEqualTo(1_000_000);

        RuleFor(request => request.QuantityOnHand)
            .GreaterThanOrEqualTo(0).WithMessage("Quantity on hand cannot be negative.")
            .LessThanOrEqualTo(1_000_000);

        RuleFor(request => request.LowStockThreshold)
            .GreaterThanOrEqualTo(0).WithMessage("Low stock threshold cannot be negative.")
            .LessThanOrEqualTo(1_000_000);
    }
}

public sealed class UpdateStockRequestValidator : AbstractValidator<UpdateStockRequest>
{
    public UpdateStockRequestValidator()
    {
        RuleFor(request => request.QuantityOnHand)
            .GreaterThanOrEqualTo(0).WithMessage("Quantity on hand cannot be negative.")
            .LessThanOrEqualTo(1_000_000);

        RuleFor(request => request.LowStockThreshold)
            .GreaterThanOrEqualTo(0).WithMessage("Low stock threshold cannot be negative.")
            .LessThanOrEqualTo(1_000_000);
    }
}

public sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator()
    {
        RuleFor(request => request.CustomerName)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(200);

        RuleFor(request => request.Items)
            .NotEmpty().WithMessage("An order needs at least one item.");

        RuleForEach(request => request.Items).ChildRules(item =>
        {
            item.RuleFor(line => line.ProductId)
                .NotEmpty().WithMessage("Product id is required.");

            item.RuleFor(line => line.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.")
                .LessThanOrEqualTo(10_000);
        });

        RuleFor(request => request.Items)
            .Must(items => items.Select(item => item.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Each product may appear on only one line.")
            .When(request => request.Items is { Count: > 0 });
    }
}
