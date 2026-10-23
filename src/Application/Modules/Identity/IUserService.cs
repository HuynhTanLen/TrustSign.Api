using TrustSign.Common;

namespace TrustSign.Application.Modules.Identity;

public interface IUserService
{
    Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancel);
}
