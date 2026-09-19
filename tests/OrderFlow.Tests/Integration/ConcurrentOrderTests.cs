using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using static OrderFlow.Tests.Integration.ApiClient;

namespace OrderFlow.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class ConcurrentOrderTests(OrderFlowApiFactory factory)
{
    /// <summary>
    /// The test the whole design exists for.
    /// </summary>
    /// <remarks>
    /// Without <c>FOR UPDATE</c> this fails. Under READ COMMITTED every request would read
    /// "10 on hand", every one would conclude four units fit, and all five would commit —
    /// selling 20 units of a product that only had 10, with no error anywhere. A
    /// transaction alone does not prevent it, because none of those writes conflict.
    /// </remarks>
    [Fact]
    public async Task Concurrent_orders_for_the_same_product_cannot_oversell_it()
    {
        const int OnHand = 10;
        const int PerOrder = 4;
        const int Attempts = 5;

        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: OnHand);

        // Fired together rather than awaited in turn, so they genuinely contend for the row.
        var responses = await Task.WhenAll(Enumerable.Range(0, Attempts).Select(attempt =>
            client.PlaceOrderAsync($"Buyer {attempt}", (product.Id, PerOrder))));

        var orders = await Task.WhenAll(
            responses.Select(response => response.Content.ReadFromJsonAsync<OrderBody>()));

        var confirmed = orders.Count(order => order!.Status == "Confirmed");
        var rejected = orders.Count(order => order!.Status == "Rejected");
        var remaining = (await client.GetProductAsync(product.Id)).QuantityOnHand;

        confirmed.Should().Be(OnHand / PerOrder, "only two lots of four fit into ten");
        rejected.Should().Be(Attempts - (OnHand / PerOrder));

        remaining.Should().Be(OnHand - (confirmed * PerOrder));
        remaining.Should().BeGreaterThanOrEqualTo(0, "stock must never go negative");

        // The real invariant, stated directly: nothing was sold that did not exist.
        ((confirmed * PerOrder) + remaining).Should().Be(OnHand);
    }

    /// <summary>
    /// Two orders naming the same two products in opposite order. Without a consistent
    /// lock order each would hold one row and wait on the other, and both would hang until
    /// PostgreSQL killed one as a deadlock victim. Locking by product id avoids that.
    /// </summary>
    [Fact]
    public async Task Orders_naming_the_same_products_in_opposite_order_do_not_deadlock()
    {
        var client = await factory.AuthenticatedAsync();
        var first = await client.CreateProductAsync(quantityOnHand: 50);
        var second = await client.CreateProductAsync(quantityOnHand: 50);

        var forwards = Enumerable.Range(0, 8).Select(i =>
            client.PlaceOrderAsync($"Forwards {i}", (first.Id, 1), (second.Id, 1)));

        var backwards = Enumerable.Range(0, 8).Select(i =>
            client.PlaceOrderAsync($"Backwards {i}", (second.Id, 1), (first.Id, 1)));

        var completed = Task.WhenAll(forwards.Concat(backwards));
        var finished = await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(30)));

        finished.Should().Be(completed, "a deadlock would leave these requests hanging");

        var responses = await completed;
        responses.Should().OnlyContain(response => response.IsSuccessStatusCode);

        (await client.GetProductAsync(first.Id)).QuantityOnHand.Should().Be(34);
        (await client.GetProductAsync(second.Id)).QuantityOnHand.Should().Be(34);
    }
}
