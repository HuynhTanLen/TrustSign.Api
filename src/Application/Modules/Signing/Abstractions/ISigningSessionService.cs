using TrustSign.Common;

namespace TrustSign.Application.Modules.Signing;

/// <summary>
/// Phiên ký qua link có token (không đăng nhập): xem phiên, OTP email (F06), ký (F07), từ chối (F08).
/// </summary>
public interface ISigningSessionService
{
    Task<Result<SigningSessionDto>> GetSessionAsync(string token, CancellationToken cancel);

    /// <summary>Gửi OTP tới email người ký; trả về thời điểm OTP hết hạn.</summary>
    Task<Result<DateTimeOffset>> RequestOtpAsync(string token, CancellationToken cancel);

    Result<OtpVerificationDto> VerifyOtp(string token, string? code);

    Task<Result> SignAsync(string token, CancellationToken cancel);

    Task<Result> DeclineAsync(string token, string? reason, CancellationToken cancel);
}
