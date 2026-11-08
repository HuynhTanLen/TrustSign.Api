using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TrustSign.Application.Modules.Contracts;
using TrustSign.Application.Modules.Notifications;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Domain.Entities.Contracts;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Contracts;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Signing;

internal sealed class SigningSessionService(
    IContractRepository contracts,
    IContractService contractService,
    IContractFileStorage fileStorage,
    ITenantRepository tenants,
    IEmailSender emailSender,
    IMemoryCache cache,
    TimeProvider timeProvider,
    ILogger<SigningSessionService> logger) : ISigningSessionService
{
    private const int MaxOtpAttempts = 5;
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VerifiedLifetime = TimeSpan.FromMinutes(30);

    public async Task<Result<SigningSessionDto>> GetSessionAsync(string token, CancellationToken cancel)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Result<(Contract Contract, ContractSigner Signer)> found = await FindSignerAsync(token, now, cancel);
        if (found.IsFailure)
        {
            return found.Error;
        }

        (Contract contract, ContractSigner signer) = found.Value;

        Tenant? tenant = await tenants.FindByIdAsync(contract.TenantId, cancel);
        string senderCompany = tenant?.Name ?? "Doanh nghiệp đối tác";

        List<SigningFieldDto> fields = contract.Signers
            .OrderBy(s => s.SignOrder)
            .Select(s => new SigningFieldDto(s.SignOrder, s.FullName, s.Id == signer.Id, s.Status == SignerStatus.Signed))
            .ToList();

        return new SigningSessionDto(
            contract.Code,
            contract.Title,
            senderCompany,
            signer.FullName,
            MaskEmail(signer.Email),
            signer.SignOrder,
            contract.Signers.Count,
            DetermineState(contract, signer, token, now),
            contract.DueAt,
            fields,
            fileStorage.CreateShortLivedDocumentUrl(contract.TenantId, contract.Id, now),
            signer.Role.ToString());
    }

    public async Task<Result<DateTimeOffset>> RequestOtpAsync(string token, CancellationToken cancel)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Result<(Contract Contract, ContractSigner Signer)> found = await FindSignerAsync(token, now, cancel);
        if (found.IsFailure)
        {
            return found.Error;
        }

        (Contract contract, ContractSigner signer) = found.Value;
        if (signer.Status != SignerStatus.Current)
        {
            return ContractErrors.NotSignerTurn;
        }

        string otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
        cache.Set(OtpKey(token), otp, OtpLifetime);

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(signer.Email, $"[Chữ Tín] Mã OTP ký hợp đồng {contract.Code}", BuildOtpEmail(signer, contract, otp)),
                cancel);
        }
        catch (Exception ex)
        {
            // Lỗi gửi mail không chặn luồng: người ký có thể bấm gửi lại OTP.
            logger.LogWarning(ex, "Không thể gửi email OTP tới {Email}", signer.Email);
        }

        return now.Add(OtpLifetime);
    }

    public Result<OtpVerificationDto> VerifyOtp(string token, string? code)
    {
        if (!fileStorage.ValidateSigningToken(token, out _, out _, timeProvider.GetUtcNow()))
        {
            return SigningErrors.TokenInvalid;
        }

        string attemptsKey = AttemptsKey(token);
        int attemptsLeft = cache.GetOrCreate(attemptsKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = OtpLifetime;
            return MaxOtpAttempts;
        });

        if (attemptsLeft <= 0)
        {
            return new OtpVerificationDto(false, 0);
        }

        string otpKey = OtpKey(token);
        if (cache.TryGetValue(otpKey, out string? expectedOtp) &&
            string.Equals(expectedOtp, code?.Trim(), StringComparison.Ordinal))
        {
            cache.Remove(otpKey);
            cache.Remove(attemptsKey);
            cache.Set(VerifiedKey(token), true, VerifiedLifetime);
            return new OtpVerificationDto(true, MaxOtpAttempts);
        }

        attemptsLeft = Math.Max(0, attemptsLeft - 1);
        cache.Set(attemptsKey, attemptsLeft, OtpLifetime);
        return new OtpVerificationDto(false, attemptsLeft);
    }

    public async Task<Result> SignAsync(string token, string? signaturePng, CancellationToken cancel)
    {
        if (!fileStorage.ValidateSigningToken(token, out Guid contractId, out Guid signerId, timeProvider.GetUtcNow()))
        {
            return SigningErrors.TokenInvalid;
        }

        if (!IsOtpVerified(token))
        {
            return SigningErrors.OtpRequired;
        }

        // Giữ cờ đã xác thực OTP (tự hết sau 5 phút) để lần bấm ký thứ hai nhận về thành công thay vì bị đòi OTP.
        return await contractService.ProcessSignOrApproveAsync(contractId, signerId, signaturePng, cancel);
    }

    public async Task<Result> DeclineAsync(string token, string? reason, CancellationToken cancel)
    {
        if (!fileStorage.ValidateSigningToken(token, out Guid contractId, out Guid signerId, timeProvider.GetUtcNow()))
        {
            return SigningErrors.TokenInvalid;
        }

        string finalReason = string.IsNullOrWhiteSpace(reason) ? "Người nhận từ chối ký hợp đồng" : reason.Trim();
        return await contractService.DeclineAsync(contractId, signerId, finalReason, cancel);
    }

    private async Task<Result<(Contract Contract, ContractSigner Signer)>> FindSignerAsync(
        string token,
        DateTimeOffset now,
        CancellationToken cancel)
    {
        if (!fileStorage.ValidateSigningToken(token, out Guid contractId, out Guid signerId, now))
        {
            return SigningErrors.TokenInvalid;
        }

        Contract? contract = await contracts.FindByIdAsync(contractId, cancel);
        if (contract is null)
        {
            return ContractErrors.ContractNotFound;
        }

        ContractSigner? signer = contract.Signers.FirstOrDefault(s => s.Id == signerId);
        if (signer is null)
        {
            return ContractErrors.SignerNotFound;
        }

        return (contract, signer);
    }

    private string DetermineState(Contract contract, ContractSigner signer, string token, DateTimeOffset now)
    {
        if (contract.Status is ContractStatus.Cancelled or ContractStatus.Expired or ContractStatus.Declined ||
            (contract.DueAt.HasValue && now > contract.DueAt.Value))
        {
            return "Expired";
        }

        return signer.Status switch
        {
            SignerStatus.Signed => "Signed",
            SignerStatus.Declined => "Declined",
            SignerStatus.Current => IsOtpVerified(token) ? "ReadyToSign" : "AwaitingOtp",
            _ => "NotYourTurn",
        };
    }

    private bool IsOtpVerified(string token) =>
        cache.TryGetValue(VerifiedKey(token), out bool verified) && verified;

    private static string OtpKey(string token) => $"signing_otp:{token.Trim()}";

    private static string VerifiedKey(string token) => $"signing_verified:{token.Trim()}";

    private static string AttemptsKey(string token) => $"signing_attempts:{token.Trim()}";

    private static string BuildOtpEmail(ContractSigner signer, Contract contract, string otp)
    {
        string safeSigner = WebUtility.HtmlEncode(signer.FullName);
        string safeTitle = WebUtility.HtmlEncode(contract.Title);
        return $"""
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
    }

    internal static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        int atIndex = email.IndexOf('@');
        if (atIndex <= 2) return "***" + email[Math.Max(0, atIndex)..];
        return email[..2] + "***" + email[atIndex..];
    }
}
