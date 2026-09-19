using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using static OrderFlow.Tests.Integration.ApiClient;

namespace OrderFlow.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class OrderPagingTests(OrderFlowApiFactory factory)
{
    private sealed record PagedOrders(
        IReadOnlyList<OrderBody> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages,
        bool HasPreviousPage,
        bool HasNextPage);

    private static async Task SeedOrdersAsync(HttpClient client, int count)
    {
        var product = await client.CreateProductAsync(quantityOnHand: count * 2);

        for (var i = 0; i < count; i++)
        {
            (await client.PlaceOrderAsync($"Pager {i}", (product.Id, 1))).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Returns_at_most_the_requested_page_size()
    {
        var client = await factory.AuthenticatedAsync();
        await SeedOrdersAsync(client, 5);

        var page = await client.GetFromJsonAsync<PagedOrders>("/api/orders?page=1&pageSize=3");

        page!.PageSize.Should().Be(3);
        page.Items.Should().HaveCountLessThanOrEqualTo(3);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task Page_size_is_clamped_rather_than_rejected()
    {
        var client = await factory.AuthenticatedAsync();

        var page = await client.GetFromJsonAsync<PagedOrders>("/api/orders?pageSize=5000");

        page!.PageSize.Should().Be(100, "an oversized page must not be allowed to scan the table");
        page.Items.Should().HaveCountLessThanOrEqualTo(100);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task A_page_below_one_is_treated_as_the_first_page(int page)
    {
        var client = await factory.AuthenticatedAsync();

        var result = await client.GetFromJsonAsync<PagedOrders>($"/api/orders?page={page}");

        result!.Page.Should().Be(1);
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_rather_than_an_error()
    {
        var client = await factory.AuthenticatedAsync();

        var result = await client.GetFromJsonAsync<PagedOrders>("/api/orders?page=100000&pageSize=10");

        result!.Items.Should().BeEmpty();
        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task Filtering_by_status_returns_only_that_status()
    {
        var client = await factory.AuthenticatedAsync();
        var product = await client.CreateProductAsync(quantityOnHand: 1);
        await client.PlaceOrderAsync("Confirmed buyer", (product.Id, 1));
        await client.PlaceOrderAsync("Rejected buyer", (product.Id, 99));

        var rejected = await client.GetFromJsonAsync<PagedOrders>("/api/orders?status=Rejected&pageSize=100");

        rejected!.Items.Should().NotBeEmpty();
        rejected.Items.Should().OnlyContain(order => order.Status == "Rejected");
    }

    [Fact]
    public async Task The_status_filter_is_case_insensitive()
    {
        var client = await factory.AuthenticatedAsync();

        var response = await client.GetAsync("/api/orders?status=confirmed");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unknown_status_is_a_bad_request()
    {
        var client = await factory.AuthenticatedAsync();

        var response = await client.GetAsync("/api/orders?status=Banana");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Confirmed");
    }

    /// <summary>
    /// Walks every page and checks each order is seen exactly once.
    /// </summary>
    /// <remarks>
    /// Guards the property that matters to a caller walking the history: every order is
    /// returned, and none twice.
    /// <para>
    /// Note what this test does and does not show. It passes with or without the
    /// <c>ThenBy(Id)</c> tiebreaker in the repository, because CreatedAt has microsecond
    /// precision and these orders do not collide. The tiebreaker is there for the case
    /// this test cannot produce on demand — tied timestamps, where SQL gives no guarantee
    /// about relative order and two queries may disagree.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Paging_through_every_page_returns_each_order_exactly_once()
    {
        var client = await factory.AuthenticatedAsync();
        await SeedOrdersAsync(client, 12);

        const int PageSize = 5;
        var seen = new List<Guid>();
        var page = 1;
        int totalCount;

        while (true)
        {
            var result = await client.GetFromJsonAsync<PagedOrders>(
                $"/api/orders?page={page}&pageSize={PageSize}");

            seen.AddRange(result!.Items.Select(order => order.Id));
            totalCount = result.TotalCount;

            if (!result.HasNextPage)
            {
                break;
            }

            page++;
        }

        seen.Should().OnlyHaveUniqueItems("no order may appear on two pages");
        seen.Should().HaveCount(totalCount, "no order may be skipped between pages");
    }
}
