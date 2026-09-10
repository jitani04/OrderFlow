using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Domain.Common;

namespace OrderFlow.Api.Infrastructure;

/// <summary>
/// Turns a broken domain rule into a 400 with a readable body instead of a bare 500.
/// </summary>
public sealed class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            // Not ours; fall through to the default handler, which does not leak internals.
            return false;
        }

        logger.LogWarning(domainException, "Rejected a request that broke a domain rule.");

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = "The request could not be completed.",
                Detail = domainException.Message,
                Status = StatusCodes.Status400BadRequest,
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
            },
            cancellationToken);

        return true;
    }
}
