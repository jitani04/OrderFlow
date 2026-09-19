using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace OrderFlow.Tests.Integration;

/// <summary>Small helpers so each test reads as the scenario it describes.</summary>
internal static class ApiClient
{
    public static async Task<HttpClient> AuthenticatedAsync(
        this OrderFlowApiFactory factory,
        string username = "admin",
        string password = "admin123")
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginBody>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        return client;
    }

    /// <summary>
    /// Creates a product with a unique SKU and known stock. Integration tests share one
    /// database, so each scenario works against its own product rather than the seeded
    /// catalogue — otherwise tests would interfere through stock levels.
    /// </summary>
    public static async Task<ProductBody> CreateProductAsync(
        this HttpClient client,
        int quantityOnHand,
        decimal price = 10.00m,
        int lowStockThreshold = 0)
    {
        var sku = $"TEST-{Guid.NewGuid():N}"[..20];

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            sku,
            name = $"Test product {sku}",
            price,
            quantityOnHand,
            lowStockThreshold,
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProductBody>())!;
    }

    public static Task<HttpResponseMessage> PlaceOrderAsync(
        this HttpClient client,
        string customerName,
        params (Guid ProductId, int Quantity)[] lines) =>
        client.PostAsJsonAsync("/api/orders", new
        {
            customerName,
            items = lines.Select(line => new { productId = line.ProductId, quantity = line.Quantity }),
        });

    public static async Task<ProductBody> GetProductAsync(this HttpClient client, Guid productId) =>
        (await client.GetFromJsonAsync<ProductBody>($"/api/products/{productId}"))!;

    /// <summary>
    /// Replaces a product's stock, carrying the version in If-Match. Pass
    /// <paramref name="version"/> as null to send no header at all.
    /// </summary>
    public static Task<HttpResponseMessage> UpdateStockAsync(
        this HttpClient client,
        Guid productId,
        int quantityOnHand,
        int lowStockThreshold,
        Guid? version)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/products/{productId}/stock")
        {
            Content = JsonContent.Create(new { quantityOnHand, lowStockThreshold }),
        };

        if (version is { } value)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{value}\"");
        }

        return client.SendAsync(request);
    }

    internal sealed record LoginBody(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, string Username, string Role);

    internal sealed record ProductBody(
        Guid Id,
        string Sku,
        string Name,
        decimal Price,
        int QuantityOnHand,
        int LowStockThreshold,
        bool IsLow,
        DateTimeOffset CreatedAt,
        Guid Version);

    internal sealed record OrderBody(
        Guid Id,
        string CustomerName,
        string Status,
        decimal TotalAmount,
        DateTimeOffset CreatedAt,
        string? RejectionReason,
        IReadOnlyList<OrderItemBody> Items);

    internal sealed record OrderItemBody(Guid ProductId, int Quantity, decimal UnitPrice, decimal LineTotal);
}
