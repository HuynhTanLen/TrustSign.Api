using TrustSign.Common;
using TrustSign.Common.Enums;

namespace TrustSign.Domain.Entities.Identity;

public sealed class User
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
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
    public Result RecordFailedLogin(DateTimeOffset now)
    {
       if(LockedUntil != null && now >= LockedUntil)
        {
            FailedLoginCount = 0;
            LockedUntil = null;
        }
       FailedLoginCount++;

       if(FailedLoginCount >= MaxFailedAttempts && LockedUntil == null )
        {
            LockedUntil = now + LockDuration;
        }

       return Result.Success();
    }

    public Result CanLogin(DateTimeOffset now)
    {
        if(LockedUntil != null && now < LockedUntil)
        {
             return IdentityErrors.AccountLocked;
        }
        return Result.Success();
    }
    
    public Result RecordSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        LockedUntil = null;
        return Result.Success();
    }
}
