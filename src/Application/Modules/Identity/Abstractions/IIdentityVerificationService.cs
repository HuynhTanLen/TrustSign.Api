using TrustSign.Common;

namespace TrustSign.Application.Modules.Identity;

public enum EkycImageKind
{
    Selfie,
    Front,
    Back
}

public interface IIdentityVerificationService
{
    Task<MyIdentityVerificationDto> GetMineAsync(Guid userId, CancellationToken cancel);

    Task<Result<MyIdentityVerificationDto>> SubmitAsync(Guid userId, SubmitIdentityVerificationCommand command, CancellationToken cancel);

    /// <summary>Tạo link ngắn hạn để làm eKYC trên điện thoại mà không cần đăng nhập ở đó.</summary>
    Task<Result<EkycHandoffDto>> CreateHandoffAsync(Guid userId, CancellationToken cancel);

    Task<Result<EkycHandoffInfoDto>> GetHandoffAsync(string token, CancellationToken cancel);

    Task<Result<MyIdentityVerificationDto>> SubmitByHandoffAsync(string token, SubmitIdentityVerificationCommand command, CancellationToken cancel);

    Task<IReadOnlyList<AdminIdentityVerificationDto>> ListAsync(CancellationToken cancel);

    Task<Result<byte[]>> GetImageAsync(Guid verificationId, EkycImageKind kind, CancellationToken cancel);

    Task<Result> RevokeAsync(Guid adminUserId, Guid verificationId, string? reason, CancellationToken cancel);
}
