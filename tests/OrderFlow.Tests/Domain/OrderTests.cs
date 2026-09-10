using FluentAssertions;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders;
using Xunit;

namespace OrderFlow.Tests.Domain;

public class OrderTests
{
    private static readonly DateTimeOffset PlacedAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Order APendingOrder() =>
        Order.Place("Ada Lovelace", [new NewOrderLine(Guid.NewGuid(), 2, 9.99m)], PlacedAt);

    [Fact]
    public void Place_starts_the_order_pending_and_totals_its_lines()
    {
        var order = Order.Place(
            "Ada Lovelace",
            [
                new NewOrderLine(Guid.NewGuid(), 2, 10.00m),
                new NewOrderLine(Guid.NewGuid(), 1, 5.50m),
            ],
            PlacedAt);

        order.Status.Should().Be(OrderStatus.Pending);
        order.CustomerName.Should().Be("Ada Lovelace");
        order.CreatedAt.Should().Be(PlacedAt);
        order.Items.Should().HaveCount(2);
        order.TotalAmount.Should().Be(25.50m);
    }

    [Fact]
    public void Place_trims_the_customer_name()
    {
        var order = Order.Place("  Ada Lovelace  ", [new NewOrderLine(Guid.NewGuid(), 1, 1m)], PlacedAt);

        order.CustomerName.Should().Be("Ada Lovelace");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Place_requires_a_customer_name(string customerName)
    {
        var place = () => Order.Place(customerName, [new NewOrderLine(Guid.NewGuid(), 1, 1m)], PlacedAt);

        place.Should().Throw<DomainException>().WithMessage("*customer name*");
    }

    [Fact]
    public void Place_requires_at_least_one_line()
    {
        var place = () => Order.Place("Ada Lovelace", [], PlacedAt);

        place.Should().Throw<DomainException>().WithMessage("*at least one line*");
    }

    [Fact]
    public void Place_rejects_the_same_product_on_two_lines()
    {
        var productId = Guid.NewGuid();

        var place = () => Order.Place(
            "Ada Lovelace",
            [new NewOrderLine(productId, 1, 1m), new NewOrderLine(productId, 2, 1m)],
            PlacedAt);

        place.Should().Throw<DomainException>().WithMessage("*more than one line*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Place_requires_a_positive_quantity(int quantity)
    {
        var place = () => Order.Place("Ada", [new NewOrderLine(Guid.NewGuid(), quantity, 1m)], PlacedAt);

        place.Should().Throw<DomainException>().WithMessage("*quantity*");
    }

    [Fact]
    public void Place_rejects_a_negative_unit_price()
    {
        var place = () => Order.Place("Ada", [new NewOrderLine(Guid.NewGuid(), 1, -0.01m)], PlacedAt);

        place.Should().Throw<DomainException>().WithMessage("*unit price*");
    }

    [Fact]
    public void Confirm_moves_a_pending_order_to_confirmed()
    {
        var order = APendingOrder();

        order.Confirm();

        order.Status.Should().Be(OrderStatus.Confirmed);
        order.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void Reject_records_the_reason()
    {
        var order = APendingOrder();

        order.Reject("Only 1 of 2 available");

        order.Status.Should().Be(OrderStatus.Rejected);
        order.RejectionReason.Should().Be("Only 1 of 2 available");
    }

    [Fact]
    public void Reject_requires_a_reason()
    {
        var order = APendingOrder();

        var reject = () => order.Reject("   ");

        reject.Should().Throw<DomainException>().WithMessage("*reason*");
    }

    [Fact]
    public void An_order_cannot_be_resolved_twice()
    {
        var confirmed = APendingOrder();
        confirmed.Confirm();

        var rejected = APendingOrder();
        rejected.Reject("Out of stock");

        confirmed.Invoking(order => order.Confirm()).Should().Throw<DomainException>();
        confirmed.Invoking(order => order.Reject("too late")).Should().Throw<DomainException>();
        rejected.Invoking(order => order.Confirm()).Should().Throw<DomainException>();
        rejected.Invoking(order => order.Reject("again")).Should().Throw<DomainException>();
    }
}
