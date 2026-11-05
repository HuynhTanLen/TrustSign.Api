using Microsoft.EntityFrameworkCore;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Domain.Repositories.Identity;

internal sealed class EfPasswordResetTokenRepository(TrustSignDbContext db) : IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancel) =>
        db.PasswordResetTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancel);

    public async Task AddAsync(PasswordResetToken token, CancellationToken cancel)
    {
        db.PasswordResetTokens.Add(token);
        await db.SaveChangesAsync(cancel);
    }

    public async Task InvalidateAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancel)
    {
        List<PasswordResetToken> active = await db.PasswordResetTokens
            .Where(token => token.UserId == userId && token.UsedAt == null && token.ExpiresAt > now)
            .ToListAsync(cancel);

        foreach (PasswordResetToken item in active)
        {
            item.Consume(now);
        }

        await db.SaveChangesAsync(cancel);
    }

    public Task SaveChangesAsync(CancellationToken cancel) =>
        db.SaveChangesAsync(cancel);
}
