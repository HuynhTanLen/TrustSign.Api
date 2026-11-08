using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSign.Api.Http;
using TrustSign.Application.Modules.Signing;
using TrustSign.Common;

namespace TrustSign.Api.Controllers.Signing;

[ApiController]
[Route("api/signing")]
[AllowAnonymous]
public sealed class SigningController(ISigningSessionService signing) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<IActionResult> GetSession(string token, CancellationToken cancel)
    {
        Result<SigningSessionDto> result = await signing.GetSessionAsync(token, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpPost("{token}/otp")]
    public async Task<IActionResult> RequestOtp(string token, CancellationToken cancel)
    {
        Result<DateTimeOffset> result = await signing.RequestOtpAsync(token, cancel);
        return result.IsSuccess ? Ok(new { expiresAt = result.Value.ToString("O") }) : this.ToProblem(result.Error);
    }

    [HttpPost("{token}/verify-otp")]
    public IActionResult VerifyOtp(string token, [FromBody] VerifySigningOtpRequestDto request)
    {
        Result<OtpVerificationDto> result = signing.VerifyOtp(token, request.Code);
        return result.IsSuccess
            ? Ok(new { verified = result.Value.Verified, attemptsLeft = result.Value.AttemptsLeft })
            : this.ToProblem(result.Error);
    }

    // IdempotencyKey chưa dùng: ký lặp đã an toàn vì người đã ký thì nhận lại thành công (TC07).
    [HttpPost("{token}/sign")]
    public async Task<IActionResult> Sign(string token, [FromBody] SignContractRequestDto request, CancellationToken cancel)
    {
        Result result = await signing.SignAsync(token, request.SignaturePng, cancel);
        return result.IsSuccess ? Ok(new { success = true }) : this.ToProblem(result.Error);
    }

    [HttpPost("{token}/decline")]
    public async Task<IActionResult> Decline(string token, [FromBody] DeclineContractRequestDto request, CancellationToken cancel)
    {
        Result result = await signing.DeclineAsync(token, request?.Reason, cancel);
        return result.IsSuccess ? Ok(new { success = true }) : this.ToProblem(result.Error);
    }
}
