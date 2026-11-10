namespace TrustSign.Application.Modules.Identity;

public sealed record RegisterRequestDto(
    string FullName,
    string Email,
    string Password,
    bool AcceptTerms = false,
    string? PhoneNumber = null);

public sealed record RegisterResultDto(string Message, string Email);

public sealed record VerifyEmailRequestDto(string Token);

public sealed record ResendVerificationRequestDto(string Email);

public sealed record ActionMessageDto(bool Success, string Message);

public sealed record LoginRequestDto(string Email, string Password);

public sealed record UpdateProfileRequestDto(
    string FullName,
    string? PhoneNumber = null,
    string? JobTitle = null);

public sealed record ForgotPasswordRequestDto(string Email);

public sealed record ResetPasswordRequestDto(string Token, string NewPassword);

public sealed record ChangePasswordRequestDto(string CurrentPassword, string NewPassword);

/// <param name="IdToken">Firebase ID token sau khi người dùng chọn tài khoản Google.</param>
/// <param name="AcceptTerms">Bắt buộc khi Google tạo tài khoản mới (NĐ 13/2023).</param>
/// <param name="Password">Chỉ cần khi email Google trùng một tài khoản đã có: chứng minh chủ tài khoản trước khi liên kết.</param>
public sealed record GoogleLoginRequestDto(string IdToken, bool AcceptTerms = false, string? Password = null);

public sealed record GoogleLinkRequestDto(string IdToken);

public sealed record AuthResponseDto(string AccessToken, DateTimeOffset ExpiresAt, UserProfileDto User);

public sealed record AuthSessionResultDto(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RawRefreshToken,
    DateTimeOffset RefreshExpiresAt,
    UserProfileDto User)
{
    public AuthResponseDto ToResponseDto() => new(AccessToken, ExpiresAt, User);
}
