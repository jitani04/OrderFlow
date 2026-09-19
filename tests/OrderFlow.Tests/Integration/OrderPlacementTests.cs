using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using static OrderFlow.Tests.Integration.ApiClient;

namespace OrderFlow.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class OrderPlacementTests(OrderFlowApiFactory factory)
{
    [Fact]
    public async Task An_order_that_fits_is_confirmed_and_deducts_stock()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 10, price: 25.00m);

        var response = await client.PlaceOrderAsync("Ada Lovelace", (product.Id, 4));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderBody>();
        order!.Status.Should().Be("Confirmed");
        order.TotalAmount.Should().Be(100.00m);
        order.RejectionReason.Should().BeNull();

        (await client.GetProductAsync(product.Id)).QuantityOnHand.Should().Be(6);
    }

    [Fact]
    public async Task An_order_larger_than_stock_is_rejected_and_deducts_nothing()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 3);

        var response = await client.PlaceOrderAsync("Grace Hopper", (product.Id, 99));

        var order = await response.Content.ReadFromJsonAsync<OrderBody>();
        order!.Status.Should().Be("Rejected");
        order.RejectionReason.Should().Contain(product.Sku)
            .And.Contain("requested 99").And.Contain("available 3");

        (await client.GetProductAsync(product.Id)).QuantityOnHand.Should().Be(3);
    }

    [Fact]
    public async Task A_mixed_order_deducts_nothing_when_any_line_is_short()
    {
        var client = await factory.AuthenticatedAsync();
        var plentiful = await client.CreateProductAsync(quantityOnHand: 100);
        var scarce = await client.CreateProductAsync(quantityOnHand: 1);

        var response = await client.PlaceOrderAsync("Alan Turing", (plentiful.Id, 5), (scarce.Id, 50));

        var order = await response.Content.ReadFromJsonAsync<OrderBody>();
        order!.Status.Should().Be("Rejected");

        (await client.GetProductAsync(plentiful.Id)).QuantityOnHand
            .Should().Be(100, "an order is filled completely or not at all");
        (await client.GetProductAsync(scarce.Id)).QuantityOnHand.Should().Be(1);
    }

    [Fact]
    public async Task An_order_for_exactly_the_remaining_stock_succeeds()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 7);

        var response = await client.PlaceOrderAsync("Ada Lovelace", (product.Id, 7));

        (await response.Content.ReadFromJsonAsync<OrderBody>())!.Status.Should().Be("Confirmed");
        (await client.GetProductAsync(product.Id)).QuantityOnHand.Should().Be(0);
    }

    [Fact]
    public async Task The_order_records_the_catalogue_price_not_one_supplied_by_the_caller()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 10, price: 42.50m);

        // The request body carries a price the API should ignore entirely.
        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "Sneaky Buyer",
            items = new[] { new { productId = product.Id, quantity = 2, unitPrice = 0.01m } },
        });

        var order = await response.Content.ReadFromJsonAsync<OrderBody>();
        order!.Items.Single().UnitPrice.Should().Be(42.50m, "price comes from the catalogue");
        order.TotalAmount.Should().Be(85.00m);
    }

    [Fact]
    public async Task An_order_for_a_product_that_does_not_exist_is_a_bad_request()
    {
        var client = await factory.AuthenticatedAsync();

        var response = await client.PlaceOrderAsync("Nobody", (Guid.NewGuid(), 1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "an unknown product is a malformed request, not a rejected order");
    }

    [Fact]
    public async Task A_rejected_order_is_still_recorded_and_retrievable()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 1);

        var placed = await client.PlaceOrderAsync("Grace Hopper", (product.Id, 5));
        var order = await placed.Content.ReadFromJsonAsync<OrderBody>();

        var fetched = await client.GetFromJsonAsync<OrderBody>($"/api/orders/{order!.Id}");

        fetched!.Status.Should().Be("Rejected");
        fetched.RejectionReason.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_non_positive_quantity_is_refused_by_validation(int quantity)
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 10);

        var response = await client.PlaceOrderAsync("Ada Lovelace", (product.Id, quantity));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_order_with_no_lines_is_refused_by_validation()
    {
        var client = await factory.AuthenticatedAsync();

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "Ada Lovelace",
            items = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
