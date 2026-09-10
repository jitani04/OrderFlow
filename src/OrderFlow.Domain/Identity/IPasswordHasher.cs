namespace OrderFlow.Domain.Identity;

/// <summary>
/// Declared here and implemented in Infrastructure, so the domain can express "verify this
/// password" without taking a dependency on a hashing library.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
