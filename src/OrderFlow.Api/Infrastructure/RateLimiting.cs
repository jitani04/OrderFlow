using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace OrderFlow.Api.Infrastructure;

public static class RateLimiting
{
    public const string LoginPolicy = "login";
    public const string RegisterPolicy = "register";

    public static IServiceCollection AddOrderFlowRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        var options = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
                      ?? new RateLimitOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy(LoginPolicy, context => Partition(
                context, "login", options.LoginPermitLimit, options.LoginWindowSeconds));

            limiter.AddPolicy(RegisterPolicy, context => Partition(
                context, "register", options.RegisterPermitLimit, options.RegisterWindowSeconds));

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                // Tell the caller when to come back. Without it a well-behaved client can
                // only guess, and typically guesses "immediately".
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Title = "Too many requests.",
                        Detail = "You have made too many attempts. Wait a moment and try again.",
                        Status = StatusCodes.Status429TooManyRequests,
                    },
                    cancellationToken);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> Partition(
        HttpContext context,
        string prefix,
        int permitLimit,
        int windowSeconds) =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Prefixed so the login and registration allowances are separate buckets for
            // the same caller rather than one shared pool.
            partitionKey: $"{prefix}:{ClientKey(context)}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),

                // No queue: a caller over the limit is told so immediately rather than
                // being held open, which would tie up a connection and hide the problem.
                QueueLimit = 0,
            });

    /// <summary>
    /// Identifies the caller for partitioning.
    /// </summary>
    /// <remarks>
    /// This is the part that makes or breaks rate limiting behind a proxy. nginx sits in
    /// front of the API, so <c>RemoteIpAddress</c> is nginx's address for every request —
    /// partition on that and every user in the world shares one bucket, so one attacker
    /// locks out everybody. Forwarded headers are processed earlier in the pipeline, which
    /// rewrites RemoteIpAddress to the real client.
    /// </remarks>
    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
