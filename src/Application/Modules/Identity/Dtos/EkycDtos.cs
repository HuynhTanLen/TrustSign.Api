namespace TrustSign.Application.Modules.Identity;

/// <summary>Trạng thái eKYC của chính người dùng; số CCCD đã che bớt.</summary>
public sealed record MyIdentityVerificationDto(
    string Status,
    string? MaskedCitizenId = null,
    string? FullName = null,
    DateOnly? DateOfBirth = null,
    DateTimeOffset? VerifiedAt = null,
    string? RevokeReason = null);

public sealed record SubmitIdentityVerificationCommand(
    string QrText,
    double FaceDistance,
    byte[] Selfie,
    byte[] Front,
    byte[] Back);

public sealed record AdminIdentityVerificationDto(
    Guid Id,
    Guid UserId,
    string Email,
    string AccountName,
    string CitizenId,
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string Address,
    DateOnly? IssuedOn,
    double FaceDistance,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt,
    string? RevokeReason);

public sealed record RevokeIdentityVerificationRequestDto(string? Reason);

public sealed record EkycHandoffDto(string Token, DateTimeOffset ExpiresAt);

/// <summary>Điện thoại chỉ thấy email đã che để người dùng biết đang xác minh cho tài khoản nào.</summary>
public sealed record EkycHandoffInfoDto(string MaskedEmail, string Status, DateTimeOffset ExpiresAt);
