namespace TrustSign.Common.Errors;

/// <summary>Lỗi của luồng xác thực doanh nghiệp (nộp hồ sơ, duyệt, từ chối).</summary>
public static class TenantVerificationErrors
{
    public const int MaxLicenseBytes = 5 * 1024 * 1024;
    public const int MaxRejectReasonLength = 500;

    public static readonly Error ProfileIncomplete =
        Error.Validation("TenantVerification.ProfileIncomplete", "Cần nhập đủ mã số thuế, địa chỉ và người đại diện trước khi nộp hồ sơ.");

    public static readonly Error LicenseInvalid =
        Error.Validation("TenantVerification.LicenseInvalid", "Giấy đăng ký kinh doanh phải là file PDF, JPEG hoặc PNG, tối đa 5 MB.");

    public static readonly Error AlreadySubmitted =
        Error.Conflict("TenantVerification.AlreadySubmitted", "Hồ sơ đang chờ duyệt hoặc doanh nghiệp đã được xác thực.");

    public static readonly Error TaxCodeTaken =
        Error.Conflict("TenantVerification.TaxCodeTaken", "Mã số thuế này đã được doanh nghiệp khác xác thực hoặc đang chờ duyệt.");

    public static readonly Error NotPending =
        Error.Conflict("TenantVerification.NotPending", "Doanh nghiệp này không có hồ sơ đang chờ duyệt.");

    public static readonly Error RejectReasonRequired =
        Error.Validation("TenantVerification.RejectReasonRequired", "Vui lòng nhập lý do từ chối (tối đa 500 ký tự).");

    public static readonly Error LicenseNotFound =
        Error.NotFound("TenantVerification.LicenseNotFound", "Doanh nghiệp chưa nộp giấy đăng ký kinh doanh.");

    public static readonly Error TenantNotVerified =
        Error.Forbidden("TenantVerification.NotVerified", "Doanh nghiệp chưa được xác thực nên chưa gửi ký được. Vui lòng nộp hồ sơ tại Cài đặt > Doanh nghiệp.");
}
