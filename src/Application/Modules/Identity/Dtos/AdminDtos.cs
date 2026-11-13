namespace TrustSign.Application.Modules.Identity;

public sealed record AdminLockRequestDto(string? Reason);

public sealed record AdminTenantListItemDto(
    Guid Id,
    string Name,
    string? TaxCode,
    string OwnerEmail,
    string? PlanName,
    string? SubscriptionStatus,
    int MembersCount,
    bool IsLocked,
    string? LockReason,
    DateTimeOffset CreatedAt,
    string VerificationStatus = "Unverified");

public sealed record AdminRejectVerificationRequestDto(string? Reason);

/// <summary>Trạng thái xác thực pháp nhân của một doanh nghiệp, kèm việc đã có giấy đăng ký kinh doanh để xem hay chưa.</summary>
public sealed record AdminVerificationDto(
    string Status,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? VerifiedAt,
    string? RejectReason,
    bool HasLicense);

public sealed record AdminUserListItemDto(
    Guid Id,
    string FullName,
    string Email,
    int TenantsCount,
    bool EmailVerified,
    bool IsAdmin,
    bool IsLocked,
    string? LockReason,
    DateTimeOffset CreatedAt,
    bool IdentityVerified = false);

public sealed record AdminHistoryItemDto(DateTimeOffset At, string Title, string? ActorEmail, string? Reason);

public sealed record AdminSubscriptionDto(
    string PlanName,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    int QuotaUsed,
    int QuotaLimit);

public sealed record AdminTenantMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    DateTimeOffset JoinedAt,
    bool IsLocked);

/// <summary>
/// Hồ sơ doanh nghiệp cho quản trị viên nền tảng: thông tin pháp lý, thuê bao, thành viên và số lượng hợp đồng.
/// Không có tiêu đề hay nội dung hợp đồng: đó là dữ liệu kinh doanh của khách, quản trị không cần đọc.
/// </summary>
public sealed record AdminTenantProfileDto(
    Guid Id,
    string Name,
    string? TaxCode,
    string? Address,
    string? Phone,
    string? ContactEmail,
    string? RepresentativeName,
    string? RepresentativeTitle,
    DateTimeOffset CreatedAt,
    bool IsLocked,
    string? LockReason,
    DateTimeOffset? LockedAt,
    AdminSubscriptionDto? Subscription,
    IReadOnlyList<AdminTenantMemberDto> Members,
    IReadOnlyDictionary<string, int> ContractCounts,
    IReadOnlyList<AdminHistoryItemDto> History,
    AdminVerificationDto? Verification = null);

public sealed record AdminUserTenantDto(Guid TenantId, string TenantName, string Role, DateTimeOffset JoinedAt, bool TenantLocked);

public sealed record AdminUserEkycDto(string Status, string CitizenIdMasked, string FullName, DateTimeOffset VerifiedAt);

/// <summary>
/// Hồ sơ người dùng cho quản trị viên: không có mật khẩu băm, phiên đăng nhập, ảnh CCCD (xem ở trang eKYC, có ghi nhật ký),
/// ngày sinh hay địa chỉ trên CCCD; số CCCD chỉ hiện dạng đã che.
/// </summary>
public sealed record AdminUserProfileDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? JobTitle,
    bool IsAdmin,
    string Status,
    bool EmailVerified,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TermsAcceptedAt,
    int FailedLoginCount,
    DateTimeOffset? TemporarilyLockedUntil,
    bool IsLocked,
    string? LockReason,
    DateTimeOffset? LockedAt,
    IReadOnlyList<AdminUserTenantDto> Tenants,
    AdminUserEkycDto? Ekyc,
    IReadOnlyList<AdminHistoryItemDto> History);
