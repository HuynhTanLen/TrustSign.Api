using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Domain.IRepositories.Identity;

public interface IIdentityVerificationRepository
{
    Task<IdentityVerification?> FindByIdAsync(Guid id, CancellationToken cancel);

    Task<IdentityVerification?> FindLatestByUserAsync(Guid userId, CancellationToken cancel);

    Task<bool> IsCitizenIdVerifiedAsync(string citizenId, CancellationToken cancel);

    /// <summary>Hồ sơ mới nhất trước, kèm email và họ tên tài khoản để quản trị viên đối chiếu.</summary>
    Task<IReadOnlyList<(IdentityVerification Verification, string Email, string AccountName)>> ListWithUsersAsync(
        int take,
        CancellationToken cancel);

    Task AddAsync(IdentityVerification verification, CancellationToken cancel);

    Task SaveChangesAsync(CancellationToken cancel);
}
