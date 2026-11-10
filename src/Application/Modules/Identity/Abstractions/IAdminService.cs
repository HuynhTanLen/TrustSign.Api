using TrustSign.Common;

namespace TrustSign.Application.Modules.Identity;

/// <summary>Quản trị nền tảng (F14, ADM-01): xem hồ sơ, khóa và mở khóa doanh nghiệp và người dùng.</summary>
public interface IAdminService
{
    Task<IReadOnlyList<AdminTenantListItemDto>> ListTenantsAsync(CancellationToken cancel);

    Task<Result<AdminTenantProfileDto>> GetTenantAsync(Guid tenantId, CancellationToken cancel);

    Task<Result> LockTenantAsync(Guid adminUserId, Guid tenantId, string? reason, CancellationToken cancel);

    Task<Result> UnlockTenantAsync(Guid adminUserId, Guid tenantId, CancellationToken cancel);

    Task<IReadOnlyList<AdminUserListItemDto>> ListUsersAsync(CancellationToken cancel);

    Task<Result<AdminUserProfileDto>> GetUserAsync(Guid userId, CancellationToken cancel);

    Task<Result> LockUserAsync(Guid adminUserId, Guid userId, string? reason, CancellationToken cancel);

    Task<Result> UnlockUserAsync(Guid adminUserId, Guid userId, CancellationToken cancel);
}
