using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Identity;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Identity;

public sealed class UserRepository(OrderFlowDbContext dbContext) : IUserRepository
{
    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Username == username.Trim().ToLower(), cancellationToken);
}
