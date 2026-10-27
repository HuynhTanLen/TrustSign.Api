using Microsoft.EntityFrameworkCore;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Domain.Repositories.Identity;

internal sealed class EfEmailVerificationTokenRepository(TrustSignDbContext db) : IEmailVerificationTokenRepository
{
    public Task<EmailVerificationToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancel) =>
        db.EmailVerificationTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancel);

    public async Task AddAsync(EmailVerificationToken token, CancellationToken cancel)
    {
        db.EmailVerificationTokens.Add(token);
        await db.SaveChangesAsync(cancel);
    }

    public Task SaveChangesAsync(CancellationToken cancel) =>
        db.SaveChangesAsync(cancel);
}
