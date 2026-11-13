namespace TrustSign.Application.Modules.Identity;

/// <summary>Cổng cho module khác (Contracts): người dùng đã xác minh danh tính (eKYC) hay chưa.</summary>
public interface IIdentityVerifiedChecker
{
    Task<bool> IsVerifiedAsync(Guid userId, CancellationToken cancel);

    /// <summary>Họ tên trên CCCD đã xác minh; null khi chưa xác minh hoặc hồ sơ đã bị thu hồi.</summary>
    Task<string?> GetVerifiedFullNameAsync(Guid userId, CancellationToken cancel);
}
