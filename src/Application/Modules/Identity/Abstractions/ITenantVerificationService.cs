using TrustSign.Common;

namespace TrustSign.Application.Modules.Identity;

/// <summary>Xác thực pháp nhân của doanh nghiệp: chủ doanh nghiệp nộp hồ sơ, quản trị viên nền tảng duyệt hoặc từ chối.</summary>
public interface ITenantVerificationService
{
    /// <summary>Chủ doanh nghiệp nộp giấy đăng ký kinh doanh (PDF, JPEG hoặc PNG, tối đa 5 MB); hồ sơ chuyển sang Chờ duyệt.</summary>
    Task<Result<TenantDto>> SubmitAsync(Guid tenantId, Guid actorUserId, byte[] license, CancellationToken cancel);

    Task<Result> ApproveAsync(Guid adminUserId, Guid tenantId, CancellationToken cancel);

    Task<Result> RejectAsync(Guid adminUserId, Guid tenantId, string? reason, CancellationToken cancel);

    /// <summary>Giấy đăng ký kinh doanh đã giải mã, chỉ dành cho quản trị viên nền tảng.</summary>
    Task<Result<byte[]>> GetLicenseAsync(Guid tenantId, CancellationToken cancel);
}

/// <summary>Cổng cho module khác (Contracts): doanh nghiệp đã được xác thực hay chưa.</summary>
public interface ITenantVerifiedChecker
{
    Task<bool> IsVerifiedAsync(Guid tenantId, CancellationToken cancel);
}
