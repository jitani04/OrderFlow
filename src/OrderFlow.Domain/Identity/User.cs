using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Identity;

/// <summary>
/// An account that may call the API. Only the hash is ever stored — the application never
/// holds a password it could leak or log.
/// </summary>
public class User
{
    private User()
    {
        // EF Core materialisation.
        Username = string.Empty;
        PasswordHash = string.Empty;
        Role = string.Empty;
    }

    public User(Guid id, string username, string passwordHash, string role)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainException("A user requires a username.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("A user requires a password hash.");
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            throw new DomainException("A user requires a role.");
        }

        Id = id;
        Username = username.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        Role = role;
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; }

    public string PasswordHash { get; private set; }

    public string Role { get; private set; }
}
