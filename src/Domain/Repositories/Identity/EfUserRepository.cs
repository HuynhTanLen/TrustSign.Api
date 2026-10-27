using Microsoft.EntityFrameworkCore;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Domain.Repositories.Identity;

internal sealed class EfUserRepository(TrustSignDbContext db) : IUserRepository
{
    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancel) =>
        db.Users.FirstOrDefaultAsync(user => user.Id == id, cancel);

    public async Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancel)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, User>();
        }

        return await db.Users
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancel);
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancel)
    {
        string normalized = User.NormalizeEmail(email);
        return db.Users.FirstOrDefaultAsync(user => user.Email == normalized, cancel);
    }

    public async Task AddAsync(User user, CancellationToken cancel)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(cancel);
    }

    public Task SaveChangesAsync(CancellationToken cancel) =>
        db.SaveChangesAsync(cancel);
}
