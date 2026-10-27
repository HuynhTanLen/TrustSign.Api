using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Identity;

internal static class UserMappingExtensions
{
    public static UserProfileDto ToProfileDto(
        this User user,
        IReadOnlyList<UserTenantMembership> memberships,
        Guid? activeTenantId = null)
    {
        List<UserTenantDto> tenants = memberships
            .Select(item => new UserTenantDto(item.Tenant.Id, item.Tenant.Name, item.Membership.Role.ToString()))
            .ToList();

        Guid? resolvedActive = activeTenantId is not null && tenants.Any(tenant => tenant.Id == activeTenantId)
            ? activeTenantId
            : tenants.FirstOrDefault()?.Id;

        return new UserProfileDto(
            user.Id,
            user.Email,
            user.FullName,
            user.Role.ToString(),
            user.Status != UserStatus.Pending,
            tenants.Count > 0,
            resolvedActive,
            tenants);
    }
}
