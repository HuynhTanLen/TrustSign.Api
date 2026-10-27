using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Domain.IRepositories.Identity;

public sealed record UserTenantMembership(Tenant Tenant, Membership Membership);

public interface ITenantRepository
{
    Task<Tenant?> FindByIdAsync(Guid tenantId, CancellationToken cancel);

    Task<IReadOnlyList<UserTenantMembership>> GetUserTenantsAsync(Guid userId, CancellationToken cancel);

    Task<Membership?> FindMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancel);

    Task<IReadOnlyList<Membership>> GetMembersAsync(Guid tenantId, CancellationToken cancel);

    Task AddTenantWithOwnerAsync(Tenant tenant, Membership ownerMembership, CancellationToken cancel);

    Task AddMembershipAsync(Membership membership, CancellationToken cancel);

    void RemoveMembership(Membership membership);

    Task<TenantInvitation?> FindInvitationByIdAsync(Guid invitationId, CancellationToken cancel);

    Task<TenantInvitation?> FindInvitationByTokenHashAsync(string tokenHash, CancellationToken cancel);

    Task<IReadOnlyList<TenantInvitation>> GetPendingInvitationsAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancel);

    Task AddInvitationAsync(TenantInvitation invitation, CancellationToken cancel);

    Task SaveChangesAsync(CancellationToken cancel);
}
