using TrustSign.Domain.Entities.Audit;

namespace TrustSign.Application.Modules.Audit;

public interface IAuditLogger
{
    /// <summary>
    /// Nối một sự kiện vào chuỗi băm của đối tượng (resourceType, resourceId) và lưu ngay
    /// cùng mọi thay đổi đang chờ trong DbContext, nên sự kiện và thay đổi nghiệp vụ được ghi trong một lần SaveChanges.
    /// </summary>
    Task LogAsync(
        Guid? tenantId,
        Guid? actorUserId,
        string action,
        string resourceType,
        string resourceId,
        object? payload = null,
        CancellationToken cancel = default);

    /// <summary>Chuỗi sự kiện của một đối tượng, xếp từ cũ đến mới theo liên kết PrevHash.</summary>
    Task<IReadOnlyList<AuditEvent>> ListForResourceAsync(
        Guid? tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancel = default);
}
