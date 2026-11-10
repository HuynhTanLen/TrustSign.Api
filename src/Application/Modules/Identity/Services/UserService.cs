using TrustSign.Common;
using TrustSign.Common.Errors;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Identity;

internal sealed class UserService(IUserRepository users, ITenantRepository tenants) : IUserService
{
    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, Guid? activeTenantId, CancellationToken cancel)
    {
        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return IdentityErrors.SessionInvalid;
        }

        IReadOnlyList<UserTenantMembership> memberships = await tenants.GetUserTenantsAsync(userId, cancel);
        return user.ToProfileDto(memberships, activeTenantId);
    }

    public async Task<Result<UserProfileDto>> UpdateProfileAsync(
        Guid userId,
        Guid? activeTenantId,
        string fullName,
        string? phoneNumber,
        string? jobTitle,
        CancellationToken cancel)
    {
        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return IdentityErrors.SessionInvalid;
        }

        Result update = user.UpdateProfile(fullName, phoneNumber, jobTitle);
        if (update.IsFailure)
        {
            return update.Error;
        }

        await users.SaveChangesAsync(cancel);
        IReadOnlyList<UserTenantMembership> memberships = await tenants.GetUserTenantsAsync(userId, cancel);
        return user.ToProfileDto(memberships, activeTenantId);
    }
}
