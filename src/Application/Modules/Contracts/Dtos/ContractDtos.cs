namespace TrustSign.Application.Modules.Contracts;

public sealed record ContractListItemDto(
    Guid Id,
    string Code,
    string Title,
    string Partner,
    string CreatorName,
    string Status,
    DateTimeOffset? SentAt,
    DateTimeOffset? DueAt,
    string? CurrentSigner);

public sealed record ContractSignerDto(
    int Order,
    string FullName,
    string Email,
    string Status,
    DateTimeOffset? SignedAt,
    string? DeclineReason,
    int PageNumber,
    float BoxX,
    float BoxY,
    float BoxWidth,
    float BoxHeight);

public sealed record ContractSignatureDto(
    string SignerName,
    string SignerEmail,
    string Issuer,
    string Serial,
    DateTimeOffset SignedAt,
    string? TimestampAuthority,
    bool Valid);

public sealed record ContractEventDto(
    DateTimeOffset At,
    string Title,
    string Detail,
    string Hash);

public sealed record ContractDetailDto(
    Guid Id,
    string Code,
    string Title,
    string Partner,
    string CreatorName,
    string Status,
    DateTimeOffset? SentAt,
    DateTimeOffset? DueAt,
    string? CurrentSigner,
    IReadOnlyList<ContractSignerDto> Signers,
    IReadOnlyList<ContractSignatureDto> Signatures,
    IReadOnlyList<ContractEventDto> Events,
    string? DocumentUrl);

public sealed record CreateFromTemplateRequestDto(
    Guid TemplateId,
    string Title,
    Dictionary<string, string>? Values);

public sealed record CreatedContractResponseDto(Guid ContractId);

public sealed record SetSignerItemDto(
    string FullName,
    string Email,
    int? Order = null,
    int? PageNumber = null,
    float? BoxX = null,
    float? BoxY = null,
    float? BoxWidth = null,
    float? BoxHeight = null);

public sealed record SetSignersRequestDto(IReadOnlyList<SetSignerItemDto> Signers);

public sealed record SendContractRequestDto(DateTimeOffset DueAt);
