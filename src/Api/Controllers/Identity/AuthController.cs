using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSign.Api.Errors;
using TrustSign.Api.Security;
using TrustSign.Application.Modules.Identity;
using TrustSign.Common;

namespace TrustSign.Api.Controllers.Identity;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService auth,
    IUserService users) : ControllerBase
{
    public const string RefreshTokenCookieName = "trustsign_rt";

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken cancel)
    {
        Result<RegisterResultDto> result = await auth.RegisterAsync(request, cancel);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : this.ToProblem(result.Error);
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequestDto request, CancellationToken cancel)
    {
        Result<ActionMessageDto> result = await auth.VerifyEmailAsync(request.Token, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(
        [FromBody] ResendVerificationRequestDto request,
        CancellationToken cancel)
    {
        Result<ActionMessageDto> result = await auth.ResendVerificationAsync(request.Email, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancel)
    {
        Result<AuthSessionResultDto> result = await auth.LoginAsync(request, cancel);
        if (result.IsFailure)
        {
            return this.ToProblem(result.Error);
        }

        WriteRefreshCookie(result.Value.RawRefreshToken, result.Value.RefreshExpiresAt);
        return Ok(result.Value.ToResponseDto());
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancel)
    {
        Request.Cookies.TryGetValue(RefreshTokenCookieName, out string? rawRefresh);
        Result<AuthSessionResultDto> result = await auth.RefreshAsync(rawRefresh, cancel);
        if (result.IsFailure)
        {
            DeleteRefreshCookie();
            return this.ToProblem(result.Error);
        }

        WriteRefreshCookie(result.Value.RawRefreshToken, result.Value.RefreshExpiresAt);
        return Ok(result.Value.ToResponseDto());
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancel)
    {
        Request.Cookies.TryGetValue(RefreshTokenCookieName, out string? rawRefresh);
        await auth.LogoutAsync(rawRefresh, cancel);
        DeleteRefreshCookie();
        return NoContent();
    }

    // Ba endpoint mật khẩu là POST nên đi qua hạn mức "write" của rate limiter toàn cục (30 request/phút theo user hoặc IP).
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request, CancellationToken cancel)
    {
        Result<ActionMessageDto> result = await auth.ForgotPasswordAsync(request.Email, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken cancel)
    {
        Result<ActionMessageDto> result = await auth.ResetPasswordAsync(request, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancel)
    {
        if (User.GetUserId() is not { } userId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        Result<ActionMessageDto> result = await auth.ChangePasswordAsync(userId, User.GetSessionId(), request, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancel)
    {
        if (User.GetUserId() is not { } userId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        Result<UserProfileDto> result = await users.GetProfileAsync(userId, User.GetTenantId(), cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpPatch("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request, CancellationToken cancel)
    {
        if (User.GetUserId() is not { } userId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        Result<UserProfileDto> result = await users.UpdateProfileAsync(
            userId,
            User.GetTenantId(),
            request.FullName,
            request.PhoneNumber,
            request.JobTitle,
            cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    private void WriteRefreshCookie(string rawToken, DateTimeOffset expiresAt) =>
        Response.Cookies.Append(RefreshTokenCookieName, rawToken, CreateCookieOptions(expiresAt));

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(RefreshTokenCookieName, CreateCookieOptions(expiresAt: null));

    private CookieOptions CreateCookieOptions(DateTimeOffset? expiresAt)
    {
        // Tự động thích ứng: Nếu chạy qua HTTPS/Cloudflare Tunnel -> Secure=true & SameSite=None
        // Nếu chạy local qua HTTP (http://localhost:5050) -> Secure=false & SameSite=Lax (tránh bị trình duyệt chặn do SameSite=None bắt buộc HTTPS)
        bool isHttps = Request.IsHttps;
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = expiresAt
        };
    }
}
