using TrustSign.Common.Enums;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Identity;

internal sealed class IdentityVerifiedChecker(IIdentityVerificationRepository verifications) : IIdentityVerifiedChecker
{
    public async Task<bool> IsVerifiedAsync(Guid userId, CancellationToken cancel) =>
        await GetVerifiedFullNameAsync(userId, cancel) is not null;

    public async Task<string?> GetVerifiedFullNameAsync(Guid userId, CancellationToken cancel)
    {
        Domain.Entities.Identity.IdentityVerification? latest = await verifications.FindLatestByUserAsync(userId, cancel);
        return latest?.Status == IdentityVerificationStatus.Verified ? latest.FullName : null;
    }
}
