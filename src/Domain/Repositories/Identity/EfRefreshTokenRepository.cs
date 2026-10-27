using Microsoft.EntityFrameworkCore;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Domain.Repositories.Identity;

internal sealed class EfRefreshTokenRepository(TrustSignDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancel) =>
        db.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancel);

    public Task<RefreshToken?> FindActiveBySessionIdAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancel) =>
        db.RefreshTokens.FirstOrDefaultAsync(
            token => token.SessionId == sessionId && token.ConsumedAt == null && token.RevokedAt == null && token.ExpiresAt > now,
            cancel);

    public async Task AddAsync(RefreshToken token, CancellationToken cancel)
    {
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(cancel);
    }

    public async Task RevokeSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancel)
    {
        List<RefreshToken> sessionTokens = await db.RefreshTokens
            .Where(token => token.SessionId == sessionId && token.RevokedAt == null)
            .ToListAsync(cancel);

        foreach (RefreshToken item in sessionTokens)
        {
            item.Revoke(now);
        }

        await db.SaveChangesAsync(cancel);
    }

    public async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancel)
    {
        List<RefreshToken> tokens = await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancel);

        foreach (RefreshToken item in tokens)
        {
            item.Revoke(now);
        }

        await db.SaveChangesAsync(cancel);
    }

    public Task SaveChangesAsync(CancellationToken cancel) =>
        db.SaveChangesAsync(cancel);
}
