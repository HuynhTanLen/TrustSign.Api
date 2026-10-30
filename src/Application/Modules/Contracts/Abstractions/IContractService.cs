using TrustSign.Common;
using TrustSign.Common.Enums;

namespace TrustSign.Application.Modules.Contracts;

public interface IContractService
{
    Task<Result<IReadOnlyList<ContractListItemDto>>> ListAsync(
        Guid tenantId,
        Guid actorUserId,
        TenantRole actorRole,
        string? query,
        string? status,
        CancellationToken cancel = default);

    Task<Result<ContractDetailDto>> GetByIdAsync(
        Guid tenantId,
        Guid actorUserId,
        TenantRole actorRole,
        Guid contractId,
        CancellationToken cancel = default);

    Task<Result<CreatedContractResponseDto>> CreateFromTemplateAsync(
        Guid tenantId,
        Guid userId,
        CreateFromTemplateRequestDto request,
        CancellationToken cancel = default);

    Task<Result<CreatedContractResponseDto>> UploadDraftAsync(
        Guid tenantId,
        Guid userId,
        string title,
        ReadOnlyMemory<byte> pdfBytes,
        CancellationToken cancel = default);

    Task<Result> SetSignersAsync(
        Guid tenantId,
        Guid actorUserId,
        TenantRole actorRole,
        Guid contractId,
        SetSignersRequestDto request,
        CancellationToken cancel = default);

    Task<Result> SendForSigningAsync(
        Guid tenantId,
        Guid actorUserId,
        TenantRole actorRole,
        Guid contractId,
        SendContractRequestDto request,
        CancellationToken cancel = default);

    Task<Result> CancelAsync(
        Guid tenantId,
        Guid actorUserId,
        TenantRole actorRole,
        Guid contractId,
        CancellationToken cancel = default);

    Task<Result<byte[]>> GetDocumentBytesByTokenAsync(
        Guid contractId,
        string token,
        CancellationToken cancel = default);
}
