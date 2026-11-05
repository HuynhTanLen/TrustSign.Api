using TrustSign.Domain.Entities.Audit;

namespace TrustSign.Domain.IRepositories.Audit;

public interface IAuditRepository
{
    Task<AuditEvent?> GetLatestForTenantAsync(Guid? tenantId, CancellationToken cancel);

    Task AddAsync(AuditEvent auditEvent, CancellationToken cancel);

    Task SaveChangesAsync(CancellationToken cancel);
}
