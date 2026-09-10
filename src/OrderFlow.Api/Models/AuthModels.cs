namespace OrderFlow.Api.Models;

public sealed record LoginRequest
{
    /// <example>admin</example>
    public required string Username { get; init; }

    /// <example>admin123</example>
    public required string Password { get; init; }
}

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string Username,
    string Role);
