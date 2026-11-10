namespace TrustSign.Application.Modules.Identity;

/// <summary>Danh tính Google đã được kiểm chứng chữ ký. <paramref name="Subject"/> là mã ổn định của người dùng ở nhà cung cấp.</summary>
public sealed record ExternalIdentity(string Subject, string Email, bool EmailVerified, string? Name);

public interface IExternalIdentityVerifier
{
    /// <summary>Kiểm tra ID token do Firebase cấp cho đăng nhập Google; trả null nếu token sai, hết hạn hoặc không phải Google.</summary>
    Task<ExternalIdentity?> VerifyGoogleAsync(string idToken, CancellationToken cancel);
}
