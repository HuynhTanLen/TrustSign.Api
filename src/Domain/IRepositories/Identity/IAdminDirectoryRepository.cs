using TrustSign.Common.Enums;

namespace TrustSign.Domain.IRepositories.Identity;

public sealed record AdminTenantRow(
    Guid Id,
    string Name,
    string? TaxCode,
    string? OwnerEmail,
    string? PlanName,
    SubscriptionStatus? SubscriptionStatus,
    int MembersCount,
    TenantStatus Status,
    string? LockReason,
    DateTimeOffset CreatedAt);

public sealed record AdminUserRow(
    Guid Id,
    string FullName,
    string Email,
    UserRole Role,
    UserStatus Status,
    int TenantsCount,
    string? LockReason,
    DateTimeOffset CreatedAt);

/// <summary>Truy vấn chỉ đọc cho khu quản trị nền tảng (F14), gom dữ liệu nhiều module trong một câu SQL.</summary>
public interface IAdminDirectoryRepository
{
    Task<IReadOnlyList<AdminTenantRow>> ListTenantsAsync(CancellationToken cancel);

    Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(CancellationToken cancel);

    /// <summary>Chỉ đếm hợp đồng theo trạng thái: quản trị viên nền tảng không cần đọc nội dung hợp đồng của khách.</summary>
    Task<IReadOnlyDictionary<ContractStatus, int>> CountContractsByStatusAsync(Guid tenantId, CancellationToken cancel);
}
