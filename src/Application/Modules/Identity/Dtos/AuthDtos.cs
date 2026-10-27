namespace TrustSign.Application.Modules.Identity;

public sealed record RegisterRequestDto(string FullName, string Email, string Password);

public sealed record RegisterResultDto(string Message, string Email);

public sealed record VerifyEmailRequestDto(string Token);

public sealed record ResendVerificationRequestDto(string Email);

public sealed record ActionMessageDto(bool Success, string Message);

public sealed record LoginRequestDto(string Email, string Password);

public sealed record UpdateProfileRequestDto(string FullName);

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
