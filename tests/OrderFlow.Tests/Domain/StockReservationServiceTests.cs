using FluentAssertions;
using OrderFlow.Domain.Catalog;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders;
using Xunit;

namespace OrderFlow.Tests.Domain;

public class StockReservationServiceTests
{
    private static readonly DateTimeOffset PlacedAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Widget = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Gadget = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Unknown = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static StockLevel Level(Guid productId, int onHand, int threshold = 0) =>
        new(productId, onHand, threshold);

    private static Dictionary<Guid, StockLevel> Stock(params StockLevel[] levels) =>
        levels.ToDictionary(level => level.ProductId);

    private static Order OrderFor(params (Guid ProductId, int Quantity)[] lines) =>
        Order.Place(customerId: null, 
            "Ada Lovelace",
            [.. lines.Select(line => new NewOrderLine(line.ProductId, line.Quantity, 10m))],
            PlacedAt);

    [Fact]
    public void Confirms_the_order_and_deducts_stock_when_every_line_fits()
    {
        var widget = Level(Widget, onHand: 10);
        var gadget = Level(Gadget, onHand: 5);
        var order = OrderFor((Widget, 3), (Gadget, 2));

        var outcome = StockReservationService.Reserve(order, Stock(widget, gadget));

        outcome.IsReserved.Should().BeTrue();
        outcome.Shortages.Should().BeEmpty();
        order.Status.Should().Be(OrderStatus.Confirmed);
        widget.QuantityOnHand.Should().Be(7);
        gadget.QuantityOnHand.Should().Be(3);
    }

    [Fact]
    public void Deducts_nothing_at_all_when_a_single_line_is_short()
    {
        var widget = Level(Widget, onHand: 10);
        var gadget = Level(Gadget, onHand: 1);
        var order = OrderFor((Widget, 3), (Gadget, 2));

        var outcome = StockReservationService.Reserve(order, Stock(widget, gadget));

        outcome.IsReserved.Should().BeFalse();
        order.Status.Should().Be(OrderStatus.Rejected);
        widget.QuantityOnHand.Should().Be(10, "an order is filled completely or not at all");
        gadget.QuantityOnHand.Should().Be(1);
    }

    [Fact]
    public void Rejection_reason_names_the_product_and_both_counts()
    {
        var order = OrderFor((Gadget, 4));

        var outcome = StockReservationService.Reserve(order, Stock(Level(Gadget, onHand: 1)));

        outcome.Shortages.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new StockShortage(Gadget, Requested: 4, Available: 1));
        order.RejectionReason.Should().Contain("requested 4").And.Contain("available 1");
    }

    [Fact]
    public void Rejection_reason_names_the_sku_when_a_catalogue_is_supplied()
    {
        var order = OrderFor((Gadget, 4));

        StockReservationService.Reserve(
            order,
            Stock(Level(Gadget, onHand: 1)),
            new Dictionary<Guid, string> { [Gadget] = "OF-CHAIR-01" });

        order.RejectionReason.Should().StartWith("OF-CHAIR-01 requested 4, available 1.",
            "an admin reading a rejected order needs the product, not a raw id");
        order.RejectionReason.Should().NotContain(Gadget.ToString());
    }

    [Fact]
    public void Rejection_reason_falls_back_to_the_id_without_a_catalogue()
    {
        var order = OrderFor((Gadget, 4));

        StockReservationService.Reserve(order, Stock(Level(Gadget, onHand: 1)));

        order.RejectionReason.Should().Contain(Gadget.ToString());
    }

    [Fact]
    public void Reports_every_short_line_not_just_the_first()
    {
        var order = OrderFor((Widget, 5), (Gadget, 5));

        var outcome = StockReservationService.Reserve(
            order, Stock(Level(Widget, onHand: 1), Level(Gadget, onHand: 1)));

        outcome.Shortages.Should().HaveCount(2);
    }

    [Fact]
    public void Treats_an_unknown_product_as_a_shortage_rather_than_an_error()
    {
        var widget = Level(Widget, onHand: 10);
        var order = OrderFor((Widget, 1), (Unknown, 1));

        var outcome = StockReservationService.Reserve(order, Stock(widget));

        outcome.IsReserved.Should().BeFalse();
        outcome.Shortages.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new StockShortage(Unknown, Requested: 1, Available: 0));
        widget.QuantityOnHand.Should().Be(10);
    }

    [Fact]
    public void Ordering_exactly_what_is_on_hand_succeeds_and_empties_the_shelf()
    {
        var widget = Level(Widget, onHand: 4);
        var order = OrderFor((Widget, 4));

        var outcome = StockReservationService.Reserve(order, Stock(widget));

        outcome.IsReserved.Should().BeTrue();
        widget.QuantityOnHand.Should().Be(0);
    }

    [Fact]
    public void Ordering_one_more_than_is_on_hand_is_rejected()
    {
        var widget = Level(Widget, onHand: 4);
        var order = OrderFor((Widget, 5));

        var outcome = StockReservationService.Reserve(order, Stock(widget));

        outcome.IsReserved.Should().BeFalse();
        widget.QuantityOnHand.Should().Be(4);
    }

    [Fact]
    public void A_second_order_only_sees_what_the_first_left_behind()
    {
        var widget = Level(Widget, onHand: 10);
        StockReservationService.Reserve(OrderFor((Widget, 8)), Stock(widget));

        var outcome = StockReservationService.Reserve(OrderFor((Widget, 5)), Stock(widget));

        outcome.IsReserved.Should().BeFalse();
        outcome.Shortages.Single().Available.Should().Be(2);
    }

    [Fact]
    public void Deducting_can_take_a_product_below_its_low_stock_threshold()
    {
        var widget = Level(Widget, onHand: 10, threshold: 3);

        StockReservationService.Reserve(OrderFor((Widget, 8)), Stock(widget));

        widget.QuantityOnHand.Should().Be(2);
        widget.IsLow.Should().BeTrue();
    }

    [Fact]
    public void An_order_that_is_already_resolved_cannot_be_reserved_again()
    {
        var widget = Level(Widget, onHand: 10);
        var order = OrderFor((Widget, 1));
        StockReservationService.Reserve(order, Stock(widget));

        var again = () => StockReservationService.Reserve(order, Stock(widget));

        again.Should().Throw<DomainException>().WithMessage("*already Confirmed*");
        widget.QuantityOnHand.Should().Be(9, "a repeat call must not deduct twice");
    }
}
