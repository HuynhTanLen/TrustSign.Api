using TrustSign.Common;
using TrustSign.Common.Enums;

namespace TrustSign.Domain.Entities.Identity;

public sealed class User
{
    public const string DefaultTermsVersion = "2026-11";
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

    public string? PhoneNumber { get; private set; }

    public string? JobTitle { get; private set; }

    public DateTimeOffset? TermsAcceptedAt { get; private set; }

    public string? TermsVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static User Register(
        string email,
        string fullName,
        string passwordHash,
        DateTimeOffset now,
        string? phoneNumber = null,
        DateTimeOffset? termsAcceptedAt = null,
        string? termsVersion = null) => new()
    {
        Id = Guid.CreateVersion7(now),
        Email = NormalizeEmail(email),
        FullName = fullName.Trim(),
        PasswordHash = passwordHash,
        Role = UserRole.User,
        Status = UserStatus.Pending,
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
        TermsAcceptedAt = termsAcceptedAt ?? now,
        TermsVersion = string.IsNullOrWhiteSpace(termsVersion) ? DefaultTermsVersion : termsVersion.Trim(),
        CreatedAt = now
    };

    /// <summary>Quản trị nền tảng do hệ thống tạo sẵn nên không cần xác minh email.</summary>
    public static User CreateAdmin(string email, string fullName, string passwordHash, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        Email = NormalizeEmail(email),
        FullName = fullName.Trim(),
        PasswordHash = passwordHash,
        Role = UserRole.Admin,
        Status = UserStatus.Active,
        TermsAcceptedAt = now,
        TermsVersion = DefaultTermsVersion,
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
        if (Status == UserStatus.Pending)
        {
            return IdentityErrors.EmailNotVerified;
        }

        if (LockedUntil != null && now >= LockedUntil)
        {
            FailedLoginCount = 0;
            LockedUntil = null;
        }

        FailedLoginCount++;

        if (FailedLoginCount >= MaxFailedAttempts && LockedUntil == null)
        {
            LockedUntil = now + LockDuration;
        }

        return Result.Success();
    }

    public Result CanLogin(DateTimeOffset now)
    {
        if (Status == UserStatus.Pending)
        {
            return IdentityErrors.EmailNotVerified;
        }
        if (Status == UserStatus.Locked)
        {
            return IdentityErrors.AccountDisabled;
        }
        if (LockedUntil != null && now < LockedUntil)
        {
            return IdentityErrors.AccountLocked;
        }
        return Result.Success();
    }

    public Result RecordSuccessfulLogin(DateTimeOffset now)
    {
        _ = now;
        FailedLoginCount = 0;
        LockedUntil = null;
        return Result.Success();
    }

    /// <summary>Đổi mật khẩu khi người dùng đã đăng nhập và nhập đúng mật khẩu cũ.</summary>
    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
    }

    /// <summary>
    /// Đặt lại mật khẩu qua email: chủ hộp thư đã chứng minh quyền sở hữu tài khoản nên xóa luôn trạng thái khóa đăng nhập.
    /// </summary>
    public void ResetPassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    public Result UpdateProfile(string fullName, string? phoneNumber = null, string? jobTitle = null)
    {
        string trimmed = fullName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 100)
        {
            return IdentityErrors.InvalidFullName;
        }

        if (!string.IsNullOrWhiteSpace(phoneNumber) && !PhoneValidator.IsValid(phoneNumber))
        {
            return IdentityErrors.InvalidPhoneNumber;
        }

        if (!string.IsNullOrWhiteSpace(jobTitle) && jobTitle.Trim().Length > 100)
        {
            return IdentityErrors.InvalidJobTitle;
        }

        FullName = trimmed;
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim();
        return Result.Success();
    }
}
