namespace TrustSign.Domain.Entities.Identity;

/// <summary>
/// Refresh token xoay vòng theo phiên đăng nhập (<see cref="SessionId"/>).
/// Dùng lại một token đã được xoay vòng (<see cref="ConsumedAt"/> khác null) sẽ bị coi là lộ token và thu hồi cả phiên (TC11).
/// </summary>
public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Doanh nghiệp người dùng đang chọn trong phiên đăng nhập này.</summary>
    public Guid? ActiveTenantId { get; private set; }

    /// <summary>Băm SHA-256 dạng hex chữ thường của refresh token thô trong cookie.</summary>
    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public bool IsConsumed => ConsumedAt is not null;

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActive(DateTimeOffset now) => ConsumedAt is null && RevokedAt is null && now < ExpiresAt;

    public static RefreshToken IssueNewSession(
        Guid userId,
        Guid? activeTenantId,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime) => new()
    {
        Id = Guid.CreateVersion7(now),
        SessionId = Guid.CreateVersion7(now),
        UserId = userId,
        ActiveTenantId = activeTenantId,
        TokenHash = tokenHash,
        ExpiresAt = now + lifetime,
        CreatedAt = now
    };

    public RefreshToken Rotate(string nextTokenHash, Guid? activeTenantId, DateTimeOffset now, TimeSpan lifetime)
    {
        ConsumedAt = now;
        ReplacedByTokenHash = nextTokenHash;

        return new RefreshToken
        {
            Id = Guid.CreateVersion7(now),
            SessionId = SessionId,
            UserId = UserId,
            ActiveTenantId = activeTenantId,
            TokenHash = nextTokenHash,
            ExpiresAt = now + lifetime,
            CreatedAt = now
        };
    }

    public void SetActiveTenant(Guid? tenantId)
    {
        ActiveTenantId = tenantId;
    }

    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now;
    }
}
