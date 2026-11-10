namespace TrustSign.Application.Modules.Signing;

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
    string? DocumentUrl,
    string Role,
    bool SenderVerified = false,
    string? SenderTaxCode = null);

public sealed record OtpVerificationDto(bool Verified, int AttemptsLeft);
