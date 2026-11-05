using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Domain.IRepositories.Identity;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancel);

    Task AddAsync(PasswordResetToken token, CancellationToken cancel);

    /// <summary>Đánh dấu đã dùng mọi token còn hiệu lực của người dùng (khi phát token mới hoặc đã đặt lại xong).</summary>
    Task InvalidateAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancel);

    Task SaveChangesAsync(CancellationToken cancel);
}
