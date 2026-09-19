using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using static OrderFlow.Tests.Integration.ApiClient;

namespace OrderFlow.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class AuthTests(OrderFlowApiFactory factory)
{
    [Fact]
    public async Task Endpoints_refuse_an_unauthenticated_caller()
    {
        var client = factory.CreateClient();

        (await client.GetAsync("/api/products")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/orders")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("admin", "wrong-password")]
    [InlineData("does-not-exist", "admin123")]
    public async Task Bad_credentials_are_refused(string username, string password)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { username, password });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_valid_login_returns_a_usable_token()
    {
        var client = await factory.AuthenticatedAsync();

        var me = await client.GetAsync("/auth/me");

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        (await me.Content.ReadAsStringAsync()).Should().Contain("admin");
    }

    [Fact]
    public async Task A_tampered_token_is_refused()
    {
        var client = await factory.AuthenticatedAsync();
        var original = client.DefaultRequestHeaders.Authorization!.Parameter;

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", original + "x");

        (await client.GetAsync("/api/products")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_endpoints_are_open()
    {
        var client = factory.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

[Collection(IntegrationCollection.Name)]
public class ProductTests(OrderFlowApiFactory factory)
{
    [Fact]
    public async Task The_seeded_catalogue_is_present()
    {
        var client = await factory.AuthenticatedAsync();

        var products = await client.GetFromJsonAsync<List<ProductBody>>("/api/products");

        products.Should().NotBeNull();
        products!.Select(product => product.Sku).Should().Contain("OF-KEYB-01");
    }

    [Fact]
    public async Task A_duplicate_sku_is_a_conflict()
    {
        var client = await factory.AuthenticatedAsync();
        var existing = await client.CreateProductAsync(quantityOnHand: 5);

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            sku = existing.Sku,
            name = "Another product with the same SKU",
            price = 1.00m,
            quantityOnHand = 1,
            lowStockThreshold = 0,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Stock_can_be_replaced_and_drives_the_low_flag()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 50, lowStockThreshold: 5);

        var response = await client.PutAsJsonAsync(
            $"/api/products/{product.Id}/stock",
            new { quantityOnHand = 4, lowStockThreshold = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await client.GetProductAsync(product.Id);
        updated.QuantityOnHand.Should().Be(4);
        updated.IsLow.Should().BeTrue("four is at or below the threshold of five");
    }

    [Fact]
    public async Task Negative_stock_is_refused()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 5);

        var response = await client.PutAsJsonAsync(
            $"/api/products/{product.Id}/stock",
            new { quantityOnHand = -1, lowStockThreshold = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Stock_for_a_product_that_does_not_exist_is_not_found()
    {
        var client = await factory.AuthenticatedAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/products/{Guid.NewGuid()}/stock",
            new { quantityOnHand = 1, lowStockThreshold = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
