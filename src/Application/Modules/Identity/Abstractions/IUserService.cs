using TrustSign.Common;

namespace TrustSign.Application.Modules.Identity;

public interface IUserService
{
    Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, Guid? activeTenantId, CancellationToken cancel);

    Task<Result<UserProfileDto>> UpdateProfileAsync(Guid userId, Guid? activeTenantId, string fullName, CancellationToken cancel);
}
