using System.Security.Cryptography;
using System.Text;
using TrustSign.Common;

namespace TrustSign.Domain.Entities.Audit;

/// <summary>
/// Nhật ký kiểm toán chuỗi băm (F11): mỗi sự kiện ghi nhận một thao tác nghiệp vụ quan trọng,
/// lưu mã băm của sự kiện trước đó (PrevHash) để phát hiện can thiệp lịch sử kiểm toán.
/// </summary>
public sealed class AuditEvent
{
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    private AuditEvent()
    {
    }

    public Guid Id { get; private set; }

    public Guid? TenantId { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string? ActorEmail { get; private set; }

    public string Action { get; private set; } = null!;

    public string ResourceType { get; private set; } = null!;

    public string ResourceId { get; private set; } = null!;

    public string? PayloadJson { get; private set; }

    public string PrevHash { get; private set; } = null!;

    public string Hash { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public static AuditEvent Create(
        Guid? tenantId,
        Guid? actorUserId,
        string? actorEmail,
        string action,
        string resourceType,
        string resourceId,
        string? payloadJson,
        string? prevHash,
        DateTimeOffset now)
    {
        Guid id = Guid.CreateVersion7(now);
        string normalizedPrevHash = string.IsNullOrWhiteSpace(prevHash)
            ? GenesisHash
            : prevHash.Trim().ToLowerInvariant();

        string normalizedAction = action.Trim();
        string normalizedResourceType = resourceType.Trim();
        string normalizedResourceId = resourceId.Trim();

        string dataToHash = $"{id:N}|{tenantId}|{actorUserId}|{normalizedAction}|{normalizedResourceType}|{normalizedResourceId}|{payloadJson}|{normalizedPrevHash}|{now.ToUnixTimeMilliseconds()}";
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(dataToHash));
        string hash = Convert.ToHexStringLower(hashBytes);

        return new AuditEvent
        {
            Id = id,
            TenantId = tenantId,
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = normalizedAction,
            ResourceType = normalizedResourceType,
            ResourceId = normalizedResourceId,
            PayloadJson = payloadJson,
            PrevHash = normalizedPrevHash,
            Hash = hash,
            CreatedAt = now
        };
    }
}
