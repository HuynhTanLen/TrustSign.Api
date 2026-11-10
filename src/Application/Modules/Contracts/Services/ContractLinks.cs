using TrustSign.Application.Modules.Media;

namespace TrustSign.Application.Modules.Contracts;

/// <summary>Token và URL gắn với hợp đồng, dựng trên <see cref="IMediaSigner"/> để Contracts và Signing dùng chung một cách.</summary>
public static class ContractLinks
{
    private static readonly TimeSpan SigningLinkLifetime = TimeSpan.FromDays(7);

    public static string CreateDocumentUrl(this IMediaSigner signer, Guid tenantId, Guid contractId, DateTimeOffset now) =>
        signer.CreateUrl(
            MediaKinds.ContractDocuments,
            contractId,
            signer.CreateToken(MediaPurposes.ContractDocument, tenantId, contractId, signer.UrlLifetime, now));

    public static bool TryValidateDocumentToken(
        this IMediaSigner signer, string token, DateTimeOffset now, out Guid tenantId, out Guid contractId) =>
        signer.TryValidate(MediaPurposes.ContractDocument, token, now, out tenantId, out contractId);

    public static string CreateSigningToken(this IMediaSigner signer, Guid contractId, Guid signerId, DateTimeOffset now) =>
        signer.CreateToken(MediaPurposes.Signing, contractId, signerId, SigningLinkLifetime, now);

    public static bool TryValidateSigningToken(
        this IMediaSigner signer, string token, DateTimeOffset now, out Guid contractId, out Guid signerId) =>
        signer.TryValidate(MediaPurposes.Signing, token, now, out contractId, out signerId);
}
