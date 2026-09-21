using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrderFlow.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class RateLimitTests(OrderFlowApiFactory factory)
{
    private const int Limit = 3;

    /// <summary>
    /// A host with tight limits, so the allowance can actually be exhausted in a test
    /// without the rest of the suite running into it.
    /// </summary>
    private WebApplicationFactory<Program> Tightened() =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:LoginPermitLimit", Limit.ToString());
            builder.UseSetting("RateLimiting:RegisterPermitLimit", Limit.ToString());
            builder.UseSetting("RateLimiting:LoginWindowSeconds", "60");
            builder.UseSetting("RateLimiting:RegisterWindowSeconds", "60");
        });

    /// <summary>
    /// Buckets are partitioned by client address, so a distinct forwarded address gives
    /// each test its own allowance and they cannot interfere with one another.
    /// </summary>
    private static HttpClient ClientFrom(WebApplicationFactory<Program> host, string address)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", address);
        return client;
    }

    private static Task<HttpResponseMessage> AttemptLoginAsync(HttpClient client) =>
        client.PostAsJsonAsync("/auth/login", new { username = "admin", password = "wrong-password" });

    [Fact]
    public async Task Repeated_sign_in_attempts_are_eventually_refused()
    {
        using var host = Tightened();
        var client = ClientFrom(host, "203.0.113.10");

        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < Limit + 2; attempt++)
        {
            statuses.Add((await AttemptLoginAsync(client)).StatusCode);
        }

        statuses.Take(Limit).Should().AllBeEquivalentTo(HttpStatusCode.Unauthorized,
            "the allowance is spent on genuine attempts first");
        statuses.Skip(Limit).Should().AllBeEquivalentTo(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// The test that justifies the forwarded-headers handling.
    /// </summary>
    /// <remarks>
    /// nginx sits in front of the API, so without forwarded headers every request arrives
    /// from the proxy's address and all callers share a single bucket — one attacker would
    /// lock out every user.
    /// <para>
    /// Confirmed to be load-bearing: with TrustForwardedHeaders set to false, the bystander
    /// here receives 429 instead of 401.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task One_client_exhausting_its_allowance_does_not_affect_another()
    {
        using var host = Tightened();
        var attacker = ClientFrom(host, "203.0.113.20");
        var bystander = ClientFrom(host, "203.0.113.21");

        for (var attempt = 0; attempt < Limit + 2; attempt++)
        {
            await AttemptLoginAsync(attacker);
        }

        (await AttemptLoginAsync(attacker)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        (await AttemptLoginAsync(bystander)).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a second client must have its own allowance");
    }

    [Fact]
    public async Task A_refused_request_says_when_to_retry()
    {
        using var host = Tightened();
        var client = ClientFrom(host, "203.0.113.30");

        HttpResponseMessage? refused = null;

        for (var attempt = 0; attempt < Limit + 2 && refused is null; attempt++)
        {
            var response = await AttemptLoginAsync(client);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                refused = response;
            }
        }

        refused.Should().NotBeNull();
        refused!.Headers.RetryAfter.Should().NotBeNull("a client told only 'too many' can just retry immediately");

        var body = await refused.Content.ReadAsStringAsync();
        body.Should().Contain("Too many requests");
    }

    [Fact]
    public async Task Registration_has_its_own_allowance_separate_from_sign_in()
    {
        using var host = Tightened();
        var client = ClientFrom(host, "203.0.113.40");

        // Spend the whole sign-in allowance.
        for (var attempt = 0; attempt < Limit + 2; attempt++)
        {
            await AttemptLoginAsync(client);
        }

        (await AttemptLoginAsync(client)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var register = await client.PostAsJsonAsync("/auth/register", new
        {
            username = $"rl{Guid.NewGuid():N}"[..14],
            password = "a-good-password",
        });

        register.StatusCode.Should().Be(HttpStatusCode.Created,
            "registration is a separate bucket, not a shared pool with sign-in");
    }

    [Fact]
    public async Task The_catalogue_is_not_rate_limited()
    {
        using var host = Tightened();
        var client = ClientFrom(host, "203.0.113.50");

        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < Limit + 5; attempt++)
        {
            statuses.Add((await client.GetAsync("/api/products")).StatusCode);
        }

        statuses.Should().AllBeEquivalentTo(HttpStatusCode.OK,
            "browsing a shop is not an attack; only the auth endpoints are throttled");
    }
}
