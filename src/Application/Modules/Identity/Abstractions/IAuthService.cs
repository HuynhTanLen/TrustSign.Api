using TrustSign.Common;

namespace TrustSign.Application.Modules.Identity;

public interface IAuthService
{
    Task<Result<RegisterResultDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancel);

    Task<Result<ActionMessageDto>> VerifyEmailAsync(string rawToken, CancellationToken cancel);

    Task<Result<ActionMessageDto>> ResendVerificationAsync(string email, CancellationToken cancel);

    Task<Result<AuthSessionResultDto>> LoginAsync(LoginRequestDto request, CancellationToken cancel);

    /// <summary>Đăng nhập bằng Google (Firebase ID token): tự tạo tài khoản mới, hoặc liên kết với tài khoản cũ nếu có mật khẩu đúng.</summary>
    Task<Result<AuthSessionResultDto>> GoogleLoginAsync(GoogleLoginRequestDto request, CancellationToken cancel);

    Task<Result<ActionMessageDto>> LinkGoogleAsync(Guid userId, string idToken, CancellationToken cancel);

    Task<Result<ActionMessageDto>> UnlinkGoogleAsync(Guid userId, CancellationToken cancel);

    Task<Result<AuthSessionResultDto>> RefreshAsync(string? rawRefreshToken, CancellationToken cancel);

    Task LogoutAsync(string? rawRefreshToken, CancellationToken cancel);

    /// <summary>Luôn thành công để không lộ email nào có trong hệ thống; chỉ gửi mail khi tài khoản tồn tại và đang Active.</summary>
    Task<Result<ActionMessageDto>> ForgotPasswordAsync(string email, CancellationToken cancel);

    Task<Result<ActionMessageDto>> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancel);

    /// <summary>Đổi mật khẩu của người đang đăng nhập; giữ lại phiên hiện tại, thu hồi mọi phiên khác.</summary>
    Task<Result<ActionMessageDto>> ChangePasswordAsync(
        Guid userId,
        Guid? currentSessionId,
        ChangePasswordRequestDto request,
        CancellationToken cancel);
}
