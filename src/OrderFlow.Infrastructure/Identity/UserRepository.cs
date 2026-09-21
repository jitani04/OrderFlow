using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Identity;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Identity;

public sealed class UserRepository(OrderFlowDbContext dbContext) : IUserRepository
{
    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Username == Normalise(username), cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.Username == Normalise(username), cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    /// <summary>Matches the normalisation the User constructor applies.</summary>
    private static string Normalise(string username) => username.Trim().ToLower();
}
