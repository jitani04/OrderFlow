using System.Net;
using FluentAssertions;
using Xunit;
using static OrderFlow.Tests.Integration.ApiClient;

namespace OrderFlow.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class StockConcurrencyTests(OrderFlowApiFactory factory)
{
    private const HttpStatusCode PreconditionRequired = (HttpStatusCode)428;

    [Fact]
    public async Task An_update_without_If_Match_is_refused()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 10);

        var response = await client.UpdateStockAsync(product.Id, 99, 0, version: null);

        response.StatusCode.Should().Be(PreconditionRequired);
        (await client.GetProductAsync(product.Id)).QuantityOnHand.Should().Be(10);
    }

    [Fact]
    public async Task An_update_carrying_the_current_version_succeeds_and_moves_it_on()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 10);

        var response = await client.UpdateStockAsync(product.Id, 42, 5, product.Version);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await client.GetProductAsync(product.Id);
        updated.QuantityOnHand.Should().Be(42);
        updated.Version.Should().NotBe(product.Version, "every write moves the version on");
    }

    [Fact]
    public async Task A_second_update_using_the_first_callers_version_is_a_conflict()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 10);

        // Two admins read the same product, then both write.
        var versionBothRead = product.Version;

        (await client.UpdateStockAsync(product.Id, 120, 0, versionBothRead))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.UpdateStockAsync(product.Id, 90, 0, versionBothRead);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "the second admin never saw the first admin's change");

        (await client.GetProductAsync(product.Id)).QuantityOnHand
            .Should().Be(120, "the losing write must not silently overwrite the winning one");
    }

    /// <summary>
    /// The case that motivated this: an order moves stock while an admin is editing.
    /// </summary>
    /// <remarks>
    /// The admin's body is an absolute count, not a delta. Applying it blindly would set
    /// stock back to what it was before the order and erase the deduction — leaving the
    /// service promising goods it has already sold. The version makes that a 409.
    /// </remarks>
    [Fact]
    public async Task An_order_placed_mid_edit_invalidates_the_admins_version()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 100);

        // The admin reads the product, intending to confirm 100 on hand.
        var versionTheAdminSaw = product.Version;

        // Meanwhile a customer buys five.
        (await client.PlaceOrderAsync("Customer", (product.Id, 5))).EnsureSuccessStatusCode();
        (await client.GetProductAsync(product.Id)).QuantityOnHand.Should().Be(95);

        // The admin now submits the count they took before the order.
        var response = await client.UpdateStockAsync(product.Id, 100, 0, versionTheAdminSaw);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.GetProductAsync(product.Id)).QuantityOnHand
            .Should().Be(95, "the order's deduction must survive");
    }

    [Fact]
    public async Task A_wildcard_If_Match_is_refused()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 10);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/products/{product.Id}/stock")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { quantityOnHand = 1, lowStockThreshold = 0 }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", "*");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(PreconditionRequired,
            "a wildcard asks for the unconditional write this endpoint exists to prevent");
    }

    [Fact]
    public async Task Reading_a_product_returns_its_version_as_an_ETag()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 10);

        var response = await client.GetAsync($"/api/products/{product.Id}");

        var etag = response.Headers.ETag?.Tag.Trim('"');
        etag.Should().Be(product.Version.ToString());
    }

    [Fact]
    public async Task Concurrent_updates_all_carrying_the_same_version_let_exactly_one_through()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 10);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(attempt =>
            client.UpdateStockAsync(product.Id, 100 + attempt, 0, product.Version)));

        responses.Count(response => response.IsSuccessStatusCode)
            .Should().Be(1, "one writer wins and the rest are told to re-read");
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict)
            .Should().Be(4);
    }
}
