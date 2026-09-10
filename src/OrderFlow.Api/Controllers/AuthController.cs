using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.Auth;
using OrderFlow.Api.Models;
using OrderFlow.Domain.Identity;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("auth")]
[Produces("application/json")]
public sealed class AuthController(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    TokenService tokenService,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>A valid BCrypt hash of a value nobody knows, used only to burn equal time.</summary>
    private const string DummyHash = "$2a$12$za0oI0x8pe8av9cmag38e.sFu72gE1vGNV9j0pFM4/fzQjFGGt0K6";

    /// <summary>Exchanges credentials for a bearer token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByUsernameAsync(request.Username, cancellationToken);

        if (user is null)
        {
            // Verify against a dummy hash anyway. Returning early here would make an
            // unknown username measurably faster than a known one with a wrong password,
            // which hands an attacker a list of valid accounts.
            passwordHasher.Verify(request.Password, DummyHash);

            logger.LogWarning("Login attempt for unknown user {Username}.", request.Username);
            return InvalidCredentials();
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Login attempt for {Username} failed on password.", user.Username);
            return InvalidCredentials();
        }

        var (accessToken, expiresAt) = tokenService.CreateToken(user);

        logger.LogInformation("Issued a token for {Username}.", user.Username);

        return Ok(new LoginResponse(accessToken, "Bearer", expiresAt, user.Username, user.Role));
    }

    /// <summary>Lets a client confirm a stored token is still valid.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me() => Ok(new
    {
        username = User.Identity?.Name,
        role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
    });

    // One response for both an unknown username and a wrong password: distinguishing them
    // would confirm which accounts exist.
    private ActionResult InvalidCredentials() => Unauthorized(new ProblemDetails
    {
        Title = "Invalid credentials.",
        Status = StatusCodes.Status401Unauthorized,
    });
}
