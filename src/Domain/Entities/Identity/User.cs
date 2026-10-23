using TrustSign.Common.Enums;

namespace TrustSign.Domain.Entities.Identity;

public sealed class User
{
    private User()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Luôn ở dạng chữ thường và là duy nhất (BR01).</summary>
    public string Email { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public UserStatus Status { get; private set; }

    public int FailedLoginCount { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static User Register(string email, string fullName, string passwordHash, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        Email = NormalizeEmail(email),
        FullName = fullName.Trim(),
        PasswordHash = passwordHash,
        Role = UserRole.User,
        Status = UserStatus.Pending,
        CreatedAt = now
    };

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void MarkEmailVerified()
    {
        if (Status == UserStatus.Pending)
        {
            Status = UserStatus.Active;
        }
    }
}
