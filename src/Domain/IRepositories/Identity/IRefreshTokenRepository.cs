using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Domain.IRepositories.Identity;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancel);

    Task<RefreshToken?> FindActiveBySessionIdAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancel);

    Task AddAsync(RefreshToken token, CancellationToken cancel);

    Task RevokeSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancel);

    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancel);

    Task SaveChangesAsync(CancellationToken cancel);
}
