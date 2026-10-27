using TrustSign.Common;

namespace TrustSign.Domain.Entities.Identity;

public sealed class EmailVerificationToken
{
    private EmailVerificationToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Băm SHA-256 dạng hex chữ thường của token gửi trong email.</summary>
    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static EmailVerificationToken Create(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) => new()
    {
        Id = Guid.CreateVersion7(now),
        UserId = userId,
        TokenHash = tokenHash,
        ExpiresAt = now + lifetime,
        CreatedAt = now
    };

    public Result Consume(DateTimeOffset now)
    {
        if (UsedAt is not null || now >= ExpiresAt)
        {
            return IdentityErrors.VerificationTokenInvalid;
        }

        UsedAt = now;
        return Result.Success();
    }
}
