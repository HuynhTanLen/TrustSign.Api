using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Common.Validation;

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

    /// <summary>Null với tài khoản chỉ đăng nhập bằng Google; đặt mật khẩu qua luồng quên mật khẩu.</summary>
    public string? PasswordHash { get; private set; }

    public UserRole Role { get; private set; }

    public UserStatus Status { get; private set; }

    public int FailedLoginCount { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>Lý do quản trị viên khóa tài khoản (Status = Locked); khác LockedUntil là khóa tạm do nhập sai mật khẩu.</summary>
    public string? LockReason { get; private set; }

    public DateTimeOffset? LockedAt { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string? JobTitle { get; private set; }

    /// <summary>Khóa file ảnh đại diện trong kho Media; null là chưa có ảnh.</summary>
    public string? AvatarKey { get; private set; }

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

    /// <summary>Tài khoản tạo từ Google: Google đã xác minh email nên Active ngay và chưa có mật khẩu.</summary>
    public static User RegisterExternal(string email, string fullName, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        Email = NormalizeEmail(email),
        FullName = fullName.Trim(),
        PasswordHash = null,
        Role = UserRole.User,
        Status = UserStatus.Active,
        TermsAcceptedAt = now,
        TermsVersion = DefaultTermsVersion,
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

    /// <summary>Quản trị viên khóa tài khoản: không đăng nhập và không làm mới token được nữa (F14).</summary>
    public Result Lock(string? reason, DateTimeOffset now)
    {
        string trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > AdminErrors.MaxLockReasonLength)
        {
            return AdminErrors.LockReasonRequired;
        }

        if (Role == UserRole.Admin)
        {
            return AdminErrors.CannotLockAdmin;
        }

        // Chỉ khóa tài khoản đang hoạt động: tài khoản Pending chưa đăng nhập được, và mở khóa luôn trả về Active.
        if (Status != UserStatus.Active)
        {
            return Status == UserStatus.Locked ? AdminErrors.AlreadyLocked : AdminErrors.UserNotActive;
        }

        Status = UserStatus.Locked;
        LockReason = trimmed;
        LockedAt = now;
        return Result.Success();
    }

    public Result Unlock()
    {
        if (Status != UserStatus.Locked)
        {
            return AdminErrors.NotLocked;
        }

        Status = UserStatus.Active;
        LockReason = null;
        LockedAt = null;
        return Result.Success();
    }

    public void SetAvatarKey(string? key) => AvatarKey = key;

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
