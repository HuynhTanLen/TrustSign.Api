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

        string hash = ComputeHash(id, tenantId, actorUserId, normalizedAction, normalizedResourceType, normalizedResourceId, payloadJson, normalizedPrevHash, now);

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

    /// <summary>
    /// Kiểm tra một chuỗi sự kiện của cùng một đối tượng, đã xếp từ cũ đến mới:
    /// mỗi sự kiện phải băm lại ra đúng Hash của nó và trỏ PrevHash về sự kiện liền trước (sự kiện đầu trỏ về GenesisHash).
    /// </summary>
    public static bool VerifyChain(IReadOnlyList<AuditEvent> chain)
    {
        string expectedPrev = GenesisHash;
        foreach (AuditEvent e in chain)
        {
            string recomputed = ComputeHash(e.Id, e.TenantId, e.ActorUserId, e.Action, e.ResourceType, e.ResourceId, e.PayloadJson, e.PrevHash, e.CreatedAt);
            if (e.PrevHash != expectedPrev || e.Hash != recomputed)
            {
                return false;
            }

            expectedPrev = e.Hash;
        }

        return true;
    }

    /// <summary>
    /// Xếp các sự kiện của một đối tượng theo liên kết PrevHash, bắt đầu từ GenesisHash.
    /// Sự kiện không nối được vào chuỗi (chuỗi bị sửa hoặc xoá) được đặt cuối để VerifyChain báo hỏng.
    /// </summary>
    public static IReadOnlyList<AuditEvent> OrderChain(IEnumerable<AuditEvent> events)
    {
        List<AuditEvent> remaining = [.. events.OrderBy(e => e.CreatedAt)];
        List<AuditEvent> ordered = new(remaining.Count);
        string prev = GenesisHash;
        while (remaining.FirstOrDefault(e => e.PrevHash == prev) is { } next)
        {
            ordered.Add(next);
            remaining.Remove(next);
            prev = next.Hash;
        }

        ordered.AddRange(remaining);
        return ordered;
    }

    private static string ComputeHash(
        Guid id,
        Guid? tenantId,
        Guid? actorUserId,
        string action,
        string resourceType,
        string resourceId,
        string? payloadJson,
        string prevHash,
        DateTimeOffset createdAt)
    {
        string dataToHash = $"{id:N}|{tenantId}|{actorUserId}|{action}|{resourceType}|{resourceId}|{payloadJson}|{prevHash}|{createdAt.ToUnixTimeMilliseconds()}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(dataToHash)));
    }
}
