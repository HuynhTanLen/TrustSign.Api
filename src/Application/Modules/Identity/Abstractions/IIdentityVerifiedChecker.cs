namespace TrustSign.Application.Modules.Identity;

/// <summary>Cổng cho module khác (Contracts): người dùng đã xác minh danh tính (eKYC) hay chưa.</summary>
public interface IIdentityVerifiedChecker
{
    Task<bool> IsVerifiedAsync(Guid userId, CancellationToken cancel);
}
