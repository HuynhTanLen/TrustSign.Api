using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Common.Validation;

namespace TrustSign.Domain.Entities.Identity;

public sealed class Tenant
{
    private Tenant()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string? TaxCode { get; private set; }

    public string? Address { get; private set; }

    public string? ContactEmail { get; private set; }

    public string? RepresentativeName { get; private set; }

    public string? RepresentativeTitle { get; private set; }

    public string? Phone { get; private set; }

    public TenantStatus Status { get; private set; }

    /// <summary>Lý do quản trị viên khóa doanh nghiệp (F14); null khi đang hoạt động.</summary>
    public string? LockReason { get; private set; }

    public DateTimeOffset? LockedAt { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public TenantVerificationStatus VerificationStatus { get; private set; }

    public DateTimeOffset? VerificationSubmittedAt { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    public string? VerificationRejectReason { get; private set; }

    /// <summary>Khóa file giấy đăng ký kinh doanh (đã mã hóa) trong kho Media; null khi chưa nộp.</summary>
    public string? BusinessLicenseKey { get; private set; }

    public static string BusinessLicenseKeyFor(Guid tenantId) => $"tenants/{tenantId:D}/verification/business-license.bin";

    public static Tenant Create(
        string name,
        string? taxCode,
        string? address,
        string? contactEmail,
        Guid createdByUserId,
        DateTimeOffset now,
        string? representativeName = null,
        string? representativeTitle = null,
        string? phone = null) => new()
    {
        Id = Guid.CreateVersion7(now),
        Name = name.Trim(),
        TaxCode = string.IsNullOrWhiteSpace(taxCode) ? null : taxCode.Trim(),
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : User.NormalizeEmail(contactEmail),
        RepresentativeName = string.IsNullOrWhiteSpace(representativeName) ? null : representativeName.Trim(),
        RepresentativeTitle = string.IsNullOrWhiteSpace(representativeTitle) ? null : representativeTitle.Trim(),
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
        Status = TenantStatus.Active,
        VerificationStatus = TenantVerificationStatus.Unverified,
        CreatedByUserId = createdByUserId,
        CreatedAt = now
    };

    /// <summary>Nộp hồ sơ xác thực; hồ sơ bị từ chối được nộp lại, hồ sơ đang chờ hoặc đã duyệt thì không.</summary>
    public Result SubmitVerification(DateTimeOffset now)
    {
        if (VerificationStatus is TenantVerificationStatus.Pending or TenantVerificationStatus.Verified)
        {
            return TenantVerificationErrors.AlreadySubmitted;
        }

        if (string.IsNullOrWhiteSpace(TaxCode) ||
            string.IsNullOrWhiteSpace(Address) ||
            string.IsNullOrWhiteSpace(RepresentativeName))
        {
            return TenantVerificationErrors.ProfileIncomplete;
        }

        VerificationStatus = TenantVerificationStatus.Pending;
        VerificationSubmittedAt = now;
        VerificationRejectReason = null;
        BusinessLicenseKey = BusinessLicenseKeyFor(Id);
        return Result.Success();
    }

    public Result ApproveVerification(DateTimeOffset now)
    {
        if (VerificationStatus != TenantVerificationStatus.Pending)
        {
            return TenantVerificationErrors.NotPending;
        }

        VerificationStatus = TenantVerificationStatus.Verified;
        VerifiedAt = now;
        return Result.Success();
    }

    public Result RejectVerification(string? reason)
    {
        string trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > TenantVerificationErrors.MaxRejectReasonLength)
        {
            return TenantVerificationErrors.RejectReasonRequired;
        }

        if (VerificationStatus != TenantVerificationStatus.Pending)
        {
            return TenantVerificationErrors.NotPending;
        }

        VerificationStatus = TenantVerificationStatus.Rejected;
        VerificationRejectReason = trimmed;
        return Result.Success();
    }

    /// <summary>Quản trị viên khóa doanh nghiệp: lần cấp token sau, thành viên không còn quyền vào doanh nghiệp này.</summary>
    public Result Lock(string? reason, DateTimeOffset now)
    {
        string trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > AdminErrors.MaxLockReasonLength)
        {
            return AdminErrors.LockReasonRequired;
        }

        if (Status == TenantStatus.Locked)
        {
            return AdminErrors.AlreadyLocked;
        }

        Status = TenantStatus.Locked;
        LockReason = trimmed;
        LockedAt = now;
        return Result.Success();
    }

    public Result Unlock()
    {
        if (Status != TenantStatus.Locked)
        {
            return AdminErrors.NotLocked;
        }

        Status = TenantStatus.Active;
        LockReason = null;
        LockedAt = null;
        return Result.Success();
    }

    public Result Update(
        string name,
        string? taxCode,
        string? address,
        string? contactEmail,
        string? phone,
        string? representativeName,
        string? representativeTitle)
    {
        string trimmedName = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName) || trimmedName.Length > 200)
        {
            return IdentityErrors.InvalidTenantName;
        }

        if (!string.IsNullOrWhiteSpace(taxCode) && !TaxCodeValidator.IsValid(taxCode))
        {
            return IdentityErrors.InvalidTaxCode;
        }

        if (!string.IsNullOrWhiteSpace(contactEmail) && !EmailValidator.IsValid(contactEmail))
        {
            return IdentityErrors.InvalidEmail;
        }

        if (!string.IsNullOrWhiteSpace(phone) && !PhoneValidator.IsValid(phone))
        {
            return IdentityErrors.InvalidPhoneNumber;
        }

        if (!string.IsNullOrWhiteSpace(representativeName) && representativeName.Trim().Length > 100)
        {
            return IdentityErrors.InvalidRepresentativeName;
        }

        if (!string.IsNullOrWhiteSpace(representativeTitle) && representativeTitle.Trim().Length > 100)
        {
            return IdentityErrors.InvalidRepresentativeTitle;
        }

        // Đổi tên hoặc mã số thuế là đổi pháp nhân: hồ sơ đã nộp hoặc đã duyệt không còn đúng với thông tin mới.
        string? newTaxCode = string.IsNullOrWhiteSpace(taxCode) ? null : taxCode.Trim();
        if (VerificationStatus is TenantVerificationStatus.Pending or TenantVerificationStatus.Verified &&
            (!string.Equals(Name, trimmedName, StringComparison.Ordinal) || !string.Equals(TaxCode, newTaxCode, StringComparison.Ordinal)))
        {
            VerificationStatus = TenantVerificationStatus.Unverified;
            VerifiedAt = null;
            VerificationSubmittedAt = null;
        }

        Name = trimmedName;
        TaxCode = newTaxCode;
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : User.NormalizeEmail(contactEmail);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        RepresentativeName = string.IsNullOrWhiteSpace(representativeName) ? null : representativeName.Trim();
        RepresentativeTitle = string.IsNullOrWhiteSpace(representativeTitle) ? null : representativeTitle.Trim();
        return Result.Success();
    }
}
