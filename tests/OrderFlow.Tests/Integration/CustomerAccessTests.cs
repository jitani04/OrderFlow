using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using static OrderFlow.Tests.Integration.ApiClient;

namespace OrderFlow.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class RegistrationTests(OrderFlowApiFactory factory)
{
    [Fact]
    public async Task Registering_creates_a_customer_and_signs_them_in()
    {
        var client = factory.CreateClient();
        var username = $"reg{Guid.NewGuid():N}"[..16];

        var response = await client.PostAsJsonAsync("/auth/register", new { username, password = "a-good-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        body!.Role.Should().Be("Customer");
        body.Username.Should().Be(username.ToLowerInvariant());
        body.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_new_account_can_never_be_an_administrator()
    {
        var client = factory.CreateClient();
        var username = $"esc{Guid.NewGuid():N}"[..16];

        // The request tries to name its own role; the endpoint must ignore it entirely.
        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            username,
            password = "a-good-password",
            role = "Admin",
        });

        (await response.Content.ReadFromJsonAsync<LoginBody>())!.Role.Should().Be("Customer");
    }

    [Fact]
    public async Task A_duplicate_username_is_a_conflict()
    {
        var (_, username) = await factory.NewCustomerAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register", new { username, password = "another-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("ab", "a-good-password")]          // username too short
    [InlineData("has spaces", "a-good-password")]  // illegal characters
    [InlineData("validname", "short")]             // password too short
    public async Task Malformed_registrations_are_refused(string username, string password)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register", new { username, password });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_registered_customer_can_sign_in_again()
    {
        var (_, username) = await factory.NewCustomerAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { username, password = "customer-password" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

[Collection(IntegrationCollection.Name)]
public class CustomerAccessTests(OrderFlowApiFactory factory)
{
    private sealed record PagedOrders(IReadOnlyList<OrderBody> Items, int TotalCount);

    [Fact]
    public async Task A_customer_sees_only_their_own_orders()
    {
        var admin = await factory.AuthenticatedAsync();
        var product = await admin.CreateProductAsync(quantityOnHand: 50);

        var (alice, _) = await factory.NewCustomerAsync();
        var (bob, _) = await factory.NewCustomerAsync();

        (await alice.PlaceOrderAsync("ignored", (product.Id, 1))).EnsureSuccessStatusCode();
        (await alice.PlaceOrderAsync("ignored", (product.Id, 1))).EnsureSuccessStatusCode();
        (await bob.PlaceOrderAsync("ignored", (product.Id, 1))).EnsureSuccessStatusCode();

        var alicesOrders = await alice.GetFromJsonAsync<PagedOrders>("/api/orders?pageSize=100");
        var bobsOrders = await bob.GetFromJsonAsync<PagedOrders>("/api/orders?pageSize=100");

        alicesOrders!.TotalCount.Should().Be(2);
        bobsOrders!.TotalCount.Should().Be(1);

        var alicesIds = alicesOrders.Items.Select(order => order.Id).ToHashSet();
        bobsOrders.Items.Should().NotContain(order => alicesIds.Contains(order.Id));
    }

    [Fact]
    public async Task An_administrator_sees_every_order()
    {
        var admin = await factory.AuthenticatedAsync();
        var product = await admin.CreateProductAsync(quantityOnHand: 50);
        var (customer, _) = await factory.NewCustomerAsync();

        await customer.PlaceOrderAsync("ignored", (product.Id, 1));

        var customerOrders = await customer.GetFromJsonAsync<PagedOrders>("/api/orders?pageSize=100");
        var allOrders = await admin.GetFromJsonAsync<PagedOrders>("/api/orders?pageSize=100");

        allOrders!.TotalCount.Should().BeGreaterThan(customerOrders!.TotalCount);
    }

    [Fact]
    public async Task Another_customers_order_is_not_found_rather_than_forbidden()
    {
        var admin = await factory.AuthenticatedAsync();
        var product = await admin.CreateProductAsync(quantityOnHand: 10);

        var (alice, _) = await factory.NewCustomerAsync();
        var (bob, _) = await factory.NewCustomerAsync();

        var placed = await alice.PlaceOrderAsync("ignored", (product.Id, 1));
        var alicesOrder = await placed.Content.ReadFromJsonAsync<OrderBody>();

        var response = await bob.GetAsync($"/api/orders/{alicesOrder!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "403 would confirm the order exists, which Bob should not learn");
    }

    [Fact]
    public async Task A_customers_order_is_recorded_under_their_own_name()
    {
        var admin = await factory.AuthenticatedAsync();
        var product = await admin.CreateProductAsync(quantityOnHand: 10);
        var (customer, username) = await factory.NewCustomerAsync();

        // The body tries to claim a different name; it must be ignored.
        var placed = await customer.PlaceOrderAsync("Somebody Else", (product.Id, 1));
        var order = await placed.Content.ReadFromJsonAsync<OrderBody>();

        order!.CustomerName.Should().Be(username,
            "a customer must not be able to place an order in another person's name");
    }

    [Fact]
    public async Task An_administrator_may_record_an_order_on_someones_behalf()
    {
        var admin = await factory.AuthenticatedAsync();
        var product = await admin.CreateProductAsync(quantityOnHand: 10);

        var placed = await admin.PlaceOrderAsync("Walk-in Customer", (product.Id, 1));
        var order = await placed.Content.ReadFromJsonAsync<OrderBody>();

        order!.CustomerName.Should().Be("Walk-in Customer");
    }

    [Fact]
    public async Task A_customer_cannot_change_the_catalogue()
    {
        var admin = await factory.AuthenticatedAsync();
        var product = await admin.CreateProductAsync(quantityOnHand: 10);
        var (customer, _) = await factory.NewCustomerAsync();

        var create = await customer.PostAsJsonAsync("/api/products", new
        {
            sku = $"NOPE-{Guid.NewGuid():N}"[..12],
            name = "Should not exist",
            price = 1.00m,
            quantityOnHand = 1,
            lowStockThreshold = 0,
        });

        var updateStock = await customer.UpdateStockAsync(product.Id, 999, 0, product.Version);

        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        updateStock.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
