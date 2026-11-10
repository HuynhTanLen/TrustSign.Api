using Microsoft.EntityFrameworkCore;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Domain.Repositories.Identity;

internal sealed class EfExternalLoginRepository(TrustSignDbContext db) : IExternalLoginRepository
{
    public Task<ExternalLogin?> FindAsync(string provider, string providerUserId, CancellationToken cancel) =>
        db.ExternalLogins.FirstOrDefaultAsync(
            login => login.Provider == provider && login.ProviderUserId == providerUserId,
            cancel);

    public Task<ExternalLogin?> FindByUserAsync(Guid userId, string provider, CancellationToken cancel) =>
        db.ExternalLogins.FirstOrDefaultAsync(
            login => login.UserId == userId && login.Provider == provider,
            cancel);

    public async Task AddAsync(ExternalLogin login, CancellationToken cancel)
    {
        db.ExternalLogins.Add(login);
        await db.SaveChangesAsync(cancel);
    }

    public async Task RemoveAsync(ExternalLogin login, CancellationToken cancel)
    {
        db.ExternalLogins.Remove(login);
        await db.SaveChangesAsync(cancel);
    }
}
