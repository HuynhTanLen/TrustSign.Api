using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using TrustSign.Api.Errors;
using TrustSign.Application.Modules.Contracts;
using TrustSign.Application.Modules.Notifications;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Contracts;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Contracts;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Api.Controllers.Contracts;

public sealed record VerifySigningOtpRequestDto(string Code);
public sealed record SignContractRequestDto(string? SignaturePng, string? IdempotencyKey);
public sealed record DeclineContractRequestDto(string? Reason);

public sealed record SigningFieldDto(
    int SignerOrder,
    string SignerName,
    bool IsMine,
    bool Signed);

public sealed record SigningSessionDto(
    string ContractCode,
    string ContractTitle,
    string SenderCompany,
    string SignerName,
    string SignerEmailMasked,
    int Order,
    int TotalSigners,
    string State,
    DateTimeOffset? DueAt,
    IReadOnlyList<SigningFieldDto> Fields,
    string? DocumentUrl);

[ApiController]
[Route("api/signing")]
[AllowAnonymous]
public sealed class SigningController(
    IContractRepository contracts,
    IContractService contractService,
    IContractFileStorage fileStorage,
    ITenantRepository tenants,
    IEmailSender emailSender,
    IMemoryCache cache,
    TimeProvider timeProvider,
    ILogger<SigningController> logger) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<IActionResult> GetSession(string token, CancellationToken cancel)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!fileStorage.ValidateSigningToken(token, out Guid contractId, out Guid signerId, now))
        {
            return this.ToProblem(Error.NotFound("Signing.TokenInvalid", "Liên kết ký không hợp lệ hoặc đã hết hạn."));
        }

        Contract? contract = await contracts.FindByIdAsync(contractId, cancel);
        if (contract is null)
        {
            return this.ToProblem(ContractErrors.ContractNotFound);
        }

        ContractSigner? signer = contract.Signers.FirstOrDefault(s => s.Id == signerId);
        if (signer is null)
        {
            return this.ToProblem(ContractErrors.SignerNotFound);
        }

        Tenant? tenant = await tenants.FindByIdAsync(contract.TenantId, cancel);
        string senderCompany = tenant?.Name ?? "Doanh nghiệp đối tác";

        string state = DetermineState(contract, signer, token, now);
        string documentUrl = fileStorage.CreateShortLivedDocumentUrl(contract.TenantId, contract.Id, now);

        List<SigningFieldDto> fields = contract.Signers
            .OrderBy(s => s.SignOrder)
            .Select(s => new SigningFieldDto(
                s.SignOrder,
                s.FullName,
                s.Id == signerId,
                s.Status == SignerStatus.Signed))
            .ToList();

        SigningSessionDto session = new(
            contract.Code,
            contract.Title,
            senderCompany,
            signer.FullName,
            MaskEmail(signer.Email),
            signer.SignOrder,
            contract.Signers.Count,
            state,
            contract.DueAt,
            fields,
            documentUrl);

        return Ok(session);
    }

    [HttpPost("{token}/otp")]
    public async Task<IActionResult> RequestOtp(string token, CancellationToken cancel)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!fileStorage.ValidateSigningToken(token, out Guid contractId, out Guid signerId, now))
        {
            return this.ToProblem(Error.NotFound("Signing.TokenInvalid", "Liên kết ký không hợp lệ hoặc đã hết hạn."));
        }

        Contract? contract = await contracts.FindByIdAsync(contractId, cancel);
        if (contract is null)
        {
            return this.ToProblem(ContractErrors.ContractNotFound);
        }

        ContractSigner? signer = contract.Signers.FirstOrDefault(s => s.Id == signerId);
        if (signer is null)
        {
            return this.ToProblem(ContractErrors.SignerNotFound);
        }

        if (signer.Status != SignerStatus.Current)
        {
            return this.ToProblem(ContractErrors.NotSignerTurn);
        }

        string otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
        string otpCacheKey = $"signing_otp:{token.Trim()}";
        cache.Set(otpCacheKey, otp, TimeSpan.FromMinutes(5));

        string safeSigner = WebUtility.HtmlEncode(signer.FullName);
        string safeTitle = WebUtility.HtmlEncode(contract.Title);
        string html = $"""
            <div style="font-family:'Segoe UI',Roboto,Arial,sans-serif;max-width:560px;margin:0 auto;color:#1f2328;line-height:1.6;">
              <h2 style="color:#1b3a5c;margin-bottom:12px;">Mã OTP xác nhận ký hợp đồng</h2>
              <p>Xin chào <strong>{safeSigner}</strong>,</p>
              <p>Mã xác thực giao dịch ký hợp đồng <strong>{safeTitle}</strong> (Mã: <code>{contract.Code}</code>) của bạn là:</p>
              <div style="font-size:28px;font-weight:700;letter-spacing:6px;color:#1f5fa8;padding:16px;background:#f0f4f8;text-align:center;border-radius:6px;margin:20px 0;">
                {otp}
              </div>
              <p style="font-size:12px;color:#5f6b7a;">Mã có hiệu lực trong 5 phút. Vui lòng không chia sẻ mã này cho bất kỳ ai.</p>
            </div>
            """;

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(signer.Email, $"[Chữ Tín] Mã OTP ký hợp đồng {contract.Code}", html),
                cancel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể gửi email OTP tới {Email}", signer.Email);
        }

        return Ok(new { expiresAt = now.AddMinutes(5).ToString("O") });
    }

    [HttpPost("{token}/verify-otp")]
    public async Task<IActionResult> VerifyOtp(
        string token,
        [FromBody] VerifySigningOtpRequestDto request,
        CancellationToken cancel)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!fileStorage.ValidateSigningToken(token, out Guid contractId, out Guid signerId, now))
        {
            return this.ToProblem(Error.NotFound("Signing.TokenInvalid", "Liên kết ký không hợp lệ hoặc đã hết hạn."));
        }

        _ = cancel;
        _ = contractId;
        _ = signerId;

        string normalizedToken = token.Trim();
        string otpCacheKey = $"signing_otp:{normalizedToken}";
        string verifiedCacheKey = $"signing_verified:{normalizedToken}";
        string attemptsCacheKey = $"signing_attempts:{normalizedToken}";

        int attemptsLeft = cache.GetOrCreate(attemptsCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return 5;
        });

        if (attemptsLeft <= 0)
        {
            return Ok(new { verified = false, attemptsLeft = 0 });
        }

        if (cache.TryGetValue(otpCacheKey, out string? expectedOtp) &&
            string.Equals(expectedOtp, request.Code?.Trim(), StringComparison.Ordinal))
        {
            cache.Remove(otpCacheKey);
            cache.Remove(attemptsCacheKey);
            cache.Set(verifiedCacheKey, true, TimeSpan.FromMinutes(30));
            return Ok(new { verified = true, attemptsLeft = 5 });
        }

        attemptsLeft = Math.Max(0, attemptsLeft - 1);
        cache.Set(attemptsCacheKey, attemptsLeft, TimeSpan.FromMinutes(5));

        return Ok(new { verified = false, attemptsLeft });
    }

    [HttpPost("{token}/sign")]
    public async Task<IActionResult> Sign(
        string token,
        [FromBody] SignContractRequestDto request,
        CancellationToken cancel)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!fileStorage.ValidateSigningToken(token, out Guid contractId, out Guid signerId, now))
        {
            return this.ToProblem(Error.NotFound("Signing.TokenInvalid", "Liên kết ký không hợp lệ hoặc đã hết hạn."));
        }

        _ = request;
        string normalizedToken = token.Trim();
        string verifiedCacheKey = $"signing_verified:{normalizedToken}";

        // Người ký đã xác thực OTP thành công trước đó
        if (!cache.TryGetValue(verifiedCacheKey, out bool isVerified) || !isVerified)
        {
            return this.ToProblem(Error.Validation("Signing.OtpRequired", "Vui lòng xác thực mã OTP trước khi ký."));
        }

        Result result = await contractService.ProcessSignOrApproveAsync(contractId, signerId, cancel);
        if (result.IsFailure)
        {
            return this.ToProblem(result.Error);
        }

        cache.Remove(verifiedCacheKey);
        return Ok(new { success = true });
    }

    [HttpPost("{token}/decline")]
    public async Task<IActionResult> Decline(
        string token,
        [FromBody] DeclineContractRequestDto request,
        CancellationToken cancel)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!fileStorage.ValidateSigningToken(token, out Guid contractId, out Guid signerId, now))
        {
            return this.ToProblem(Error.NotFound("Signing.TokenInvalid", "Liên kết ký không hợp lệ hoặc đã hết hạn."));
        }

        string reason = string.IsNullOrWhiteSpace(request?.Reason) ? "Người nhận từ chối ký hợp đồng" : request.Reason.Trim();
        Result result = await contractService.DeclineAsync(contractId, signerId, reason, cancel);
        return result.IsSuccess ? Ok(new { success = true }) : this.ToProblem(result.Error);
    }

    private string DetermineState(Contract contract, ContractSigner signer, string token, DateTimeOffset now)
    {
        if (contract.Status is ContractStatus.Cancelled or ContractStatus.Expired or ContractStatus.Declined ||
            (contract.DueAt.HasValue && now > contract.DueAt.Value))
        {
            return "Expired";
        }

        if (signer.Status == SignerStatus.Signed)
        {
            return "Signed";
        }

        if (signer.Status == SignerStatus.Declined)
        {
            return "Declined";
        }

        if (signer.Status == SignerStatus.Waiting)
        {
            return "NotYourTurn";
        }

        if (signer.Status == SignerStatus.Current)
        {
            string verifiedCacheKey = $"signing_verified:{token.Trim()}";
            if (cache.TryGetValue(verifiedCacheKey, out bool verified) && verified)
            {
                return "ReadyToSign";
            }

            return "AwaitingOtp";
        }

        return "NotYourTurn";
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        int atIndex = email.IndexOf('@');
        if (atIndex <= 2) return "***" + email[Math.Max(0, atIndex)..];
        return email[..2] + "***" + email[atIndex..];
    }
}
