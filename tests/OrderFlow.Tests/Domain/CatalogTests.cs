using FluentAssertions;
using OrderFlow.Domain.Catalog;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Identity;
using Xunit;

namespace OrderFlow.Tests.Domain;

public class CatalogTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Widget = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void A_product_normalises_its_sku_and_trims_its_name()
    {
        var product = new Product(Widget, " of-keyb-01 ", "  Mechanical Keyboard ", 129.99m, CreatedAt);

        product.Sku.Should().Be("OF-KEYB-01", "SKUs are compared and looked up case-insensitively");
        product.Name.Should().Be("Mechanical Keyboard");
    }

    [Fact]
    public void A_product_requires_a_sku_a_name_and_a_non_negative_price()
    {
        var noSku = () => new Product(Widget, "  ", "Widget", 1m, CreatedAt);
        var noName = () => new Product(Widget, "SKU-1", " ", 1m, CreatedAt);
        var negativePrice = () => new Product(Widget, "SKU-1", "Widget", -0.01m, CreatedAt);

        noSku.Should().Throw<DomainException>().WithMessage("*SKU*");
        noName.Should().Throw<DomainException>().WithMessage("*name*");
        negativePrice.Should().Throw<DomainException>().WithMessage("*price*");
    }

    [Fact]
    public void Is_low_when_quantity_reaches_the_threshold()
    {
        new StockLevel(Widget, quantityOnHand: 5, lowStockThreshold: 5).IsLow
            .Should().BeTrue("the threshold is inclusive");

        new StockLevel(Widget, quantityOnHand: 6, lowStockThreshold: 5).IsLow
            .Should().BeFalse();
    }

    [Fact]
    public void Can_fulfil_only_a_positive_quantity_that_is_on_hand()
    {
        var stock = new StockLevel(Widget, quantityOnHand: 5, lowStockThreshold: 0);

        stock.CanFulfil(5).Should().BeTrue();
        stock.CanFulfil(6).Should().BeFalse();
        stock.CanFulfil(0).Should().BeFalse();
        stock.CanFulfil(-1).Should().BeFalse();
    }

    [Fact]
    public void Setting_stock_replaces_both_values()
    {
        var stock = new StockLevel(Widget, quantityOnHand: 5, lowStockThreshold: 1);

        stock.Set(quantityOnHand: 42, lowStockThreshold: 10);

        stock.QuantityOnHand.Should().Be(42);
        stock.LowStockThreshold.Should().Be(10);
    }

    [Fact]
    public void Stock_cannot_be_set_negative()
    {
        var stock = new StockLevel(Widget, quantityOnHand: 5, lowStockThreshold: 1);

        stock.Invoking(s => s.Set(-1, 0)).Should().Throw<DomainException>().WithMessage("*negative*");
        stock.Invoking(s => s.Set(1, -1)).Should().Throw<DomainException>().WithMessage("*negative*");
    }

    [Fact]
    public void A_user_normalises_its_username_and_requires_a_hash_and_role()
    {
        var user = new User(Guid.NewGuid(), " Admin ", "hash", Roles.Admin);

        user.Username.Should().Be("admin", "usernames are matched case-insensitively at login");

        var noHash = () => new User(Guid.NewGuid(), "admin", " ", Roles.Admin);
        noHash.Should().Throw<DomainException>().WithMessage("*password hash*");
    }
}
