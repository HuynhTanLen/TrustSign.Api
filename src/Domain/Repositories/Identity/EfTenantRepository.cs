using Microsoft.EntityFrameworkCore;
using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Domain.Repositories.Identity;

internal sealed class EfTenantRepository(TrustSignDbContext db) : ITenantRepository
{
    public Task<Tenant?> FindByIdAsync(Guid tenantId, CancellationToken cancel) =>
        db.Tenants.FirstOrDefaultAsync(tenant => tenant.Id == tenantId, cancel);

    public async Task<IReadOnlyList<UserTenantMembership>> GetUserTenantsAsync(Guid userId, CancellationToken cancel)
    {
        var rows = await (
            from membership in db.Memberships
            join tenant in db.Tenants on membership.TenantId equals tenant.Id
            where membership.UserId == userId
            orderby membership.JoinedAt
            select new { Tenant = tenant, Membership = membership })
            .ToListAsync(cancel);

        return rows.Select(row => new UserTenantMembership(row.Tenant, row.Membership)).ToList();
    }

    public Task<Membership?> FindMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancel) =>
        db.Memberships.FirstOrDefaultAsync(
            membership => membership.TenantId == tenantId && membership.UserId == userId,
            cancel);

    public async Task<IReadOnlyList<Membership>> GetMembersAsync(Guid tenantId, CancellationToken cancel) =>
        await db.Memberships
            .Where(membership => membership.TenantId == tenantId)
            .OrderBy(membership => membership.JoinedAt)
            .ToListAsync(cancel);

    public async Task AddTenantWithOwnerAsync(Tenant tenant, Membership ownerMembership, CancellationToken cancel)
    {
        db.Tenants.Add(tenant);
        db.Memberships.Add(ownerMembership);
        await db.SaveChangesAsync(cancel);
    }

    public async Task AddMembershipAsync(Membership membership, CancellationToken cancel)
    {
        db.Memberships.Add(membership);
        await db.SaveChangesAsync(cancel);
    }

    public void RemoveMembership(Membership membership)
    {
        db.Memberships.Remove(membership);
    }

    public Task<TenantInvitation?> FindInvitationByIdAsync(Guid invitationId, CancellationToken cancel) =>
        db.TenantInvitations.FirstOrDefaultAsync(invitation => invitation.Id == invitationId, cancel);

    public Task<TenantInvitation?> FindInvitationByTokenHashAsync(string tokenHash, CancellationToken cancel) =>
        db.TenantInvitations.FirstOrDefaultAsync(invitation => invitation.TokenHash == tokenHash, cancel);

    public async Task<IReadOnlyList<TenantInvitation>> GetPendingInvitationsAsync(
        Guid tenantId,
        DateTimeOffset now,
        CancellationToken cancel) =>
        await db.TenantInvitations
            .Where(invitation =>
                invitation.TenantId == tenantId &&
                invitation.Status == InvitationStatus.Pending &&
                invitation.ExpiresAt > now)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToListAsync(cancel);

    public async Task AddInvitationAsync(TenantInvitation invitation, CancellationToken cancel)
    {
        db.TenantInvitations.Add(invitation);
        await db.SaveChangesAsync(cancel);
    }

    public Task SaveChangesAsync(CancellationToken cancel) =>
        db.SaveChangesAsync(cancel);
}
