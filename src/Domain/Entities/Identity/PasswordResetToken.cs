using TrustSign.Common;
using TrustSign.Common.Errors;

namespace TrustSign.Domain.Entities.Identity;

/// <summary>
/// Token đặt lại mật khẩu gửi qua email. Chỉ lưu bản băm, hết hạn sau một khoảng ngắn và chỉ dùng được một lần.
/// </summary>
public sealed class PasswordResetToken
{
    private PasswordResetToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Băm SHA-256 dạng hex chữ thường của token gửi trong email.</summary>
    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static PasswordResetToken Create(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) => new()
    {
        Id = Guid.CreateVersion7(now),
        UserId = userId,
        TokenHash = tokenHash,
        ExpiresAt = now + lifetime,
        CreatedAt = now
    };

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

    public Result Consume(DateTimeOffset now)
    {
        if (!IsUsable(now))
        {
            return IdentityErrors.PasswordResetTokenInvalid;
        }

        UsedAt = now;
        return Result.Success();
    }
}
