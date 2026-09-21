using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using OrderFlow.Domain.Identity;

namespace OrderFlow.Api.Infrastructure;

/// <summary>
/// Reads the caller's identity from the validated token.
/// </summary>
/// <remarks>
/// Always from claims, never from the request body. A body field saying who you are is a
/// suggestion; a claim on a signed token is a fact.
/// </remarks>
public static class CurrentUser
{
    public static Guid? IdOf(ClaimsPrincipal principal)
    {
        // JwtBearer maps the standard "sub" claim onto NameIdentifier by default, but the
        // raw name survives when that mapping is turned off — accept either.
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static string? UsernameOf(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Name);

    public static bool IsAdmin(ClaimsPrincipal principal) => principal.IsInRole(Roles.Admin);
}
