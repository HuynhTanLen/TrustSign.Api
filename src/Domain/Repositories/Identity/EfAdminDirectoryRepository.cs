using Microsoft.EntityFrameworkCore;
using TrustSign.Common.Enums;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Domain.Repositories.Identity;

// shortcut: chưa phân trang (vài trăm doanh nghiệp/người dùng là đủ cho khóa luận); thêm skip/take khi danh sách lớn.
internal sealed class EfAdminDirectoryRepository(TrustSignDbContext db) : IAdminDirectoryRepository
{
    public async Task<IReadOnlyList<AdminTenantRow>> ListTenantsAsync(CancellationToken cancel) =>
        await db.Tenants
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                Tenant = t,
                OwnerEmail = db.Memberships
                    .Where(m => m.TenantId == t.Id && m.Role == TenantRole.TenantOwner)
                    .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => u.Email)
                    .FirstOrDefault(),
                Subscription = db.Subscriptions.FirstOrDefault(s => s.TenantId == t.Id),
                MembersCount = db.Memberships.Count(m => m.TenantId == t.Id),
            })
            .Select(x => new AdminTenantRow(
                x.Tenant.Id,
                x.Tenant.Name,
                x.Tenant.TaxCode,
                x.OwnerEmail,
                x.Subscription == null ? null : db.Plans.Where(p => p.Id == x.Subscription.PlanId).Select(p => p.Name).FirstOrDefault(),
                x.Subscription == null ? null : (SubscriptionStatus?)x.Subscription.Status,
                x.MembersCount,
                x.Tenant.Status,
                x.Tenant.LockReason,
                x.Tenant.CreatedAt,
                x.Tenant.VerificationStatus))
            .ToListAsync(cancel);

    public async Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(CancellationToken cancel) =>
        await db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminUserRow(
                u.Id,
                u.FullName,
                u.Email,
                u.Role,
                u.Status,
                db.Memberships.Count(m => m.UserId == u.Id),
                u.LockReason,
                u.CreatedAt,
                db.IdentityVerifications.Any(v => v.UserId == u.Id && v.Status == IdentityVerificationStatus.Verified)))
            .ToListAsync(cancel);

    public async Task<IReadOnlyDictionary<ContractStatus, int>> CountContractsByStatusAsync(Guid tenantId, CancellationToken cancel) =>
        await db.Contracts
            .Where(c => c.TenantId == tenantId)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancel);
}
