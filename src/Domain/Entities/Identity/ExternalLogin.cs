namespace TrustSign.Domain.Entities.Identity;

/// <summary>Liên kết giữa tài khoản và danh tính ở nhà cung cấp ngoài (Google qua Firebase). Mỗi danh tính ngoài chỉ gắn với một tài khoản.</summary>
public sealed class ExternalLogin
{
    public const string GoogleProvider = "google";

    private ExternalLogin()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Provider { get; private set; } = null!;

    /// <summary>Mã người dùng ổn định do nhà cung cấp cấp (Firebase uid).</summary>
    public string ProviderUserId { get; private set; } = null!;

    /// <summary>Email của tài khoản Google lúc liên kết; có thể khác email tài khoản TrustSign.</summary>
    public string Email { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public static ExternalLogin Create(Guid userId, string provider, string providerUserId, string email, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        UserId = userId,
        Provider = provider,
        ProviderUserId = providerUserId,
        Email = email,
        CreatedAt = now
    };
}
