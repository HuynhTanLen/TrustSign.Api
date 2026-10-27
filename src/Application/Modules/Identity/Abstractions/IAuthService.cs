using TrustSign.Common;

namespace TrustSign.Application.Modules.Identity;

public interface IAuthService
{
    Task<Result<RegisterResultDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancel);

    Task<Result<ActionMessageDto>> VerifyEmailAsync(string rawToken, CancellationToken cancel);

    Task<Result<ActionMessageDto>> ResendVerificationAsync(string email, CancellationToken cancel);

    Task<Result<AuthSessionResultDto>> LoginAsync(LoginRequestDto request, CancellationToken cancel);

    Task<Result<AuthSessionResultDto>> RefreshAsync(string? rawRefreshToken, CancellationToken cancel);

    Task LogoutAsync(string? rawRefreshToken, CancellationToken cancel);
}
