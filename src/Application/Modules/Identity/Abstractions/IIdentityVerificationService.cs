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

    Task<IReadOnlyList<AdminIdentityVerificationDto>> ListAsync(CancellationToken cancel);

    Task<Result<byte[]>> GetImageAsync(Guid verificationId, EkycImageKind kind, CancellationToken cancel);

    Task<Result> RevokeAsync(Guid adminUserId, Guid verificationId, string? reason, CancellationToken cancel);
}
