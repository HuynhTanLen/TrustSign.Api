using System.Text.Json;
using TrustSign.Application.Modules.Audit;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Domain.Entities.Audit;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.Entities.Subscriptions;
using TrustSign.Domain.IRepositories.Identity;
using TrustSign.Domain.IRepositories.Subscriptions;

namespace TrustSign.Application.Modules.Identity;

internal sealed class AdminService(
    IAdminDirectoryRepository directory,
    IUserRepository users,
    ITenantRepository tenants,
    IRefreshTokenRepository refreshTokens,
    IIdentityVerificationRepository verifications,
    ISubscriptionRepository subscriptions,
    IPlanRepository plans,
    IAuditLogger audit,
    TimeProvider timeProvider) : IAdminService
{
    private static readonly Dictionary<string, string> HistoryTitles = new()
    {
        ["Admin.TenantLocked"] = "Khóa doanh nghiệp",
        ["Admin.TenantUnlocked"] = "Mở khóa doanh nghiệp",
        ["Admin.UserLocked"] = "Khóa tài khoản",
        ["Admin.UserUnlocked"] = "Mở khóa tài khoản",
        ["Tenant.Updated"] = "Cập nhật thông tin doanh nghiệp",
    };

    private sealed record LockPayload(string? Reason);

    public async Task<IReadOnlyList<AdminTenantListItemDto>> ListTenantsAsync(CancellationToken cancel) =>
        (await directory.ListTenantsAsync(cancel))
            .Select(t => new AdminTenantListItemDto(
                t.Id,
                t.Name,
                t.TaxCode,
                t.OwnerEmail ?? string.Empty,
                t.PlanName,
                t.SubscriptionStatus?.ToString(),
                t.MembersCount,
                t.Status == TenantStatus.Locked,
                t.LockReason,
                t.CreatedAt))
            .ToList();

    public async Task<IReadOnlyList<AdminUserListItemDto>> ListUsersAsync(CancellationToken cancel) =>
        (await directory.ListUsersAsync(cancel))
            .Select(u => new AdminUserListItemDto(
                u.Id,
                u.FullName,
                u.Email,
                u.TenantsCount,
                u.Status != UserStatus.Pending,
                u.Role == UserRole.Admin,
                u.Status == UserStatus.Locked,
                u.LockReason,
                u.CreatedAt))
            .ToList();

    public async Task<Result<AdminTenantProfileDto>> GetTenantAsync(Guid tenantId, CancellationToken cancel)
    {
        Tenant? tenant = await tenants.FindByIdAsync(tenantId, cancel);
        if (tenant is null)
        {
            return AdminErrors.TenantNotFound;
        }

        IReadOnlyList<Membership> memberships = await tenants.GetMembersAsync(tenantId, cancel);
        IReadOnlyDictionary<Guid, User> memberUsers = await users.FindByIdsAsync([.. memberships.Select(m => m.UserId)], cancel);
        List<AdminTenantMemberDto> members = memberships
            .Where(m => memberUsers.ContainsKey(m.UserId))
            .Select(m => new AdminTenantMemberDto(
                m.UserId,
                memberUsers[m.UserId].FullName,
                memberUsers[m.UserId].Email,
                m.Role.ToString(),
                m.JoinedAt,
                memberUsers[m.UserId].Status == UserStatus.Locked))
            .OrderBy(m => m.Role != nameof(TenantRole.TenantOwner))
            .ThenBy(m => m.JoinedAt)
            .ToList();

        AdminSubscriptionDto? subscriptionDto = null;
        Subscription? subscription = await subscriptions.FindByTenantIdAsync(tenantId, cancel);
        if (subscription is not null)
        {
            Plan? plan = await plans.FindByIdAsync(subscription.PlanId, cancel);
            subscriptionDto = new AdminSubscriptionDto(
                plan?.Name ?? "(gói đã xóa)",
                subscription.Status.ToString(),
                subscription.StartedAt,
                subscription.ExpiresAt,
                subscription.QuotaUsed,
                subscription.QuotaLimit);
        }

        IReadOnlyDictionary<ContractStatus, int> counts = await directory.CountContractsByStatusAsync(tenantId, cancel);

        return new AdminTenantProfileDto(
            tenant.Id,
            tenant.Name,
            tenant.TaxCode,
            tenant.Address,
            tenant.Phone,
            tenant.ContactEmail,
            tenant.RepresentativeName,
            tenant.RepresentativeTitle,
            tenant.CreatedAt,
            tenant.Status == TenantStatus.Locked,
            tenant.LockReason,
            tenant.LockedAt,
            subscriptionDto,
            members,
            Enum.GetValues<ContractStatus>().ToDictionary(s => s.ToString(), s => counts.GetValueOrDefault(s)),
            await HistoryAsync(tenantId, "Tenant", tenantId, cancel));
    }

    public async Task<Result<AdminUserProfileDto>> GetUserAsync(Guid userId, CancellationToken cancel)
    {
        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return AdminErrors.UserNotFound;
        }

        List<AdminUserTenantDto> userTenants = (await tenants.GetUserTenantsAsync(userId, cancel))
            .Select(x => new AdminUserTenantDto(
                x.Tenant.Id,
                x.Tenant.Name,
                x.Membership.Role.ToString(),
                x.Membership.JoinedAt,
                x.Tenant.Status == TenantStatus.Locked))
            .ToList();

        IdentityVerification? ekyc = await verifications.FindLatestByUserAsync(userId, cancel);
        DateTimeOffset now = timeProvider.GetUtcNow();

        return new AdminUserProfileDto(
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.JobTitle,
            user.Role == UserRole.Admin,
            user.Status.ToString(),
            user.Status != UserStatus.Pending,
            user.CreatedAt,
            user.TermsAcceptedAt,
            user.FailedLoginCount,
            user.LockedUntil > now ? user.LockedUntil : null,
            user.Status == UserStatus.Locked,
            user.LockReason,
            user.LockedAt,
            userTenants,
            ekyc is null
                ? null
                : new AdminUserEkycDto(ekyc.Status.ToString(), CitizenQr.Mask(ekyc.CitizenId), ekyc.FullName, ekyc.CreatedAt),
            await HistoryAsync(null, "User", userId, cancel));
    }

    public async Task<Result> LockTenantAsync(Guid adminUserId, Guid tenantId, string? reason, CancellationToken cancel)
    {
        Tenant? tenant = await tenants.FindByIdAsync(tenantId, cancel);
        if (tenant is null)
        {
            return AdminErrors.TenantNotFound;
        }

        Result locked = tenant.Lock(reason, timeProvider.GetUtcNow());
        if (locked.IsFailure)
        {
            return locked.Error;
        }

        // LogAsync lưu chung DbContext: trạng thái khóa và nhật ký được ghi trong một lần SaveChanges.
        await audit.LogAsync(tenantId, adminUserId, "Admin.TenantLocked", "Tenant", tenantId.ToString(), new LockPayload(tenant.LockReason), cancel);
        await tenants.SaveChangesAsync(cancel);
        return Result.Success();
    }

    public async Task<Result> UnlockTenantAsync(Guid adminUserId, Guid tenantId, CancellationToken cancel)
    {
        Tenant? tenant = await tenants.FindByIdAsync(tenantId, cancel);
        if (tenant is null)
        {
            return AdminErrors.TenantNotFound;
        }

        Result unlocked = tenant.Unlock();
        if (unlocked.IsFailure)
        {
            return unlocked.Error;
        }

        await audit.LogAsync(tenantId, adminUserId, "Admin.TenantUnlocked", "Tenant", tenantId.ToString(), cancel: cancel);
        await tenants.SaveChangesAsync(cancel);
        return Result.Success();
    }

    public async Task<Result> LockUserAsync(Guid adminUserId, Guid userId, string? reason, CancellationToken cancel)
    {
        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return AdminErrors.UserNotFound;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Result locked = user.Lock(reason, now);
        if (locked.IsFailure)
        {
            return locked.Error;
        }

        // Thu hồi mọi refresh token: người bị khóa mất quyền ngay khi access token hiện tại (tối đa 15 phút) hết hạn.
        await refreshTokens.RevokeAllForUserAsync(userId, now, cancel);
        await audit.LogAsync(null, adminUserId, "Admin.UserLocked", "User", userId.ToString(), new LockPayload(user.LockReason), cancel);
        await users.SaveChangesAsync(cancel);
        return Result.Success();
    }

    public async Task<Result> UnlockUserAsync(Guid adminUserId, Guid userId, CancellationToken cancel)
    {
        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return AdminErrors.UserNotFound;
        }

        Result unlocked = user.Unlock();
        if (unlocked.IsFailure)
        {
            return unlocked.Error;
        }

        await audit.LogAsync(null, adminUserId, "Admin.UserUnlocked", "User", userId.ToString(), cancel: cancel);
        await users.SaveChangesAsync(cancel);
        return Result.Success();
    }

    private async Task<IReadOnlyList<AdminHistoryItemDto>> HistoryAsync(Guid? tenantId, string resourceType, Guid resourceId, CancellationToken cancel) =>
        (await audit.ListForResourceAsync(tenantId, resourceType, resourceId.ToString(), cancel))
            .Reverse()
            .Select(ToHistoryItem)
            .ToList();

    private static AdminHistoryItemDto ToHistoryItem(AuditEvent e)
    {
        // Chỉ đọc lý do của thao tác quản trị; payload sự kiện khác có thể chứa dữ liệu không cần hiện.
        string? reason = e.Action.StartsWith("Admin.", StringComparison.Ordinal) && e.PayloadJson is not null
            ? JsonSerializer.Deserialize<LockPayload>(e.PayloadJson)?.Reason
            : null;
        return new AdminHistoryItemDto(e.CreatedAt, HistoryTitles.GetValueOrDefault(e.Action, e.Action), e.ActorEmail, reason);
    }
}
