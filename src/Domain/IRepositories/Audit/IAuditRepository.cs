using TrustSign.Domain.Entities.Audit;

namespace TrustSign.Domain.IRepositories.Audit;

public interface IAuditRepository
{
    /// <summary>Sự kiện cuối của chuỗi một đối tượng: sự kiện chưa có sự kiện nào trỏ PrevHash về nó.</summary>
    Task<AuditEvent?> GetChainTipAsync(string resourceType, string resourceId, CancellationToken cancel);

    Task<IReadOnlyList<AuditEvent>> ListForResourceAsync(Guid? tenantId, string resourceType, string resourceId, CancellationToken cancel);

    Task AddAsync(AuditEvent auditEvent, CancellationToken cancel);

    Task SaveChangesAsync(CancellationToken cancel);
}
