using TrustSign.Common;
using TrustSign.Common.Enums;

namespace TrustSign.Domain.Entities.Identity;

/// <summary>
/// Một lần xác minh danh tính (eKYC) của tài khoản: dữ liệu đọc từ mã QR trên CCCD gắn chip
/// cùng khoá lưu trữ của ba ảnh (khuôn mặt, CCCD mặt trước, mặt sau) đã mã hoá.
/// Mỗi tài khoản và mỗi số CCCD chỉ có tối đa một bản ghi <see cref="IdentityVerificationStatus.Verified"/>.
/// </summary>
public sealed class IdentityVerification
{
    /// <summary>Ngưỡng khoảng cách Euclid giữa hai vector khuôn mặt 128 chiều; nhỏ hơn là cùng một người.</summary>
    public const double MaxFaceDistance = 0.6;

    public const int MaxImageBytes = 5 * 1024 * 1024;

    private IdentityVerification()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string CitizenId { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public DateOnly DateOfBirth { get; private set; }

    public string Gender { get; private set; } = null!;

    public string Address { get; private set; } = null!;

    public DateOnly? IssuedOn { get; private set; }

    public double FaceDistance { get; private set; }

    public IdentityVerificationStatus Status { get; private set; }

    public string SelfieKey { get; private set; } = null!;

    public string FrontKey { get; private set; } = null!;

    public string BackKey { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public string? RevokeReason { get; private set; }

    public static IdentityVerification Create(
        Guid userId,
        string citizenId,
        string fullName,
        DateOnly dateOfBirth,
        string gender,
        string address,
        DateOnly? issuedOn,
        double faceDistance,
        DateTimeOffset now)
    {
        Guid id = Guid.CreateVersion7(now);
        string folder = $"ekyc/{userId:N}/{id:N}";
        return new IdentityVerification
        {
            Id = id,
            UserId = userId,
            CitizenId = citizenId,
            FullName = fullName,
            DateOfBirth = dateOfBirth,
            Gender = gender,
            Address = address,
            IssuedOn = issuedOn,
            FaceDistance = faceDistance,
            Status = IdentityVerificationStatus.Verified,
            SelfieKey = $"{folder}/selfie.bin",
            FrontKey = $"{folder}/front.bin",
            BackKey = $"{folder}/back.bin",
            CreatedAt = now
        };
    }

    public Result Revoke(Guid adminUserId, string? reason, DateTimeOffset now)
    {
        if (Status != IdentityVerificationStatus.Verified)
        {
            return IdentityErrors.EkycNotVerified;
        }

        Status = IdentityVerificationStatus.Revoked;
        RevokedAt = now;
        RevokedByUserId = adminUserId;
        string? trimmed = reason?.Trim();
        RevokeReason = string.IsNullOrEmpty(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, 300)];
        return Result.Success();
    }
}
