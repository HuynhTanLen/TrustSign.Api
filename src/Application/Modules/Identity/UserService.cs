using TrustSign.Common;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Identity;

internal sealed class UserService(IUserRepository users) : IUserService
{
    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancel)
    {
        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return IdentityErrors.SessionInvalid;
        }

        return user.ToProfileDto();
    }
}
