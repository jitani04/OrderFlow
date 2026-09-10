using OrderFlow.Domain.Identity;

namespace OrderFlow.Infrastructure.Identity;

/// <summary>
/// BCrypt, chosen because it is deliberately slow and salts every hash automatically.
/// A general-purpose hash such as SHA-256 is the wrong tool here: it is fast, which is
/// precisely what an attacker with a stolen table wants.
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Each increment doubles the work. 12 costs a few hundred milliseconds on current
    /// hardware — slow enough to make offline cracking expensive, fast enough for a login.
    /// </summary>
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // A malformed stored hash must fail the login, not crash the request.
            return false;
        }
    }
}
