using TrustSign.Common.Enums;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Identity;

internal sealed class IdentityVerifiedChecker(IIdentityVerificationRepository verifications) : IIdentityVerifiedChecker
{
    public async Task<bool> IsVerifiedAsync(Guid userId, CancellationToken cancel) =>
        (await verifications.FindLatestByUserAsync(userId, cancel))?.Status == IdentityVerificationStatus.Verified;
}
