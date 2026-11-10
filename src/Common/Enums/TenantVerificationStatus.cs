namespace TrustSign.Common.Enums;

/// <summary>
/// Xác thực pháp nhân của doanh nghiệp (khác với <see cref="TenantStatus"/> là khóa/mở khóa):
/// Chưa nộp → Chờ duyệt → Đã xác thực, hoặc Bị từ chối và được nộp lại.
/// </summary>
public enum TenantVerificationStatus
{
    Unverified,
    Pending,
    Verified,
    Rejected
}
