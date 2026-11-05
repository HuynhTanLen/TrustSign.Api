using Microsoft.EntityFrameworkCore;
using TrustSign.Domain.Entities.Audit;
using TrustSign.Domain.IRepositories.Audit;

namespace TrustSign.Domain.Repositories.Audit;

internal sealed class EfAuditRepository(TrustSignDbContext db) : IAuditRepository
{
    public Task<AuditEvent?> GetLatestForTenantAsync(Guid? tenantId, CancellationToken cancel) =>
        db.AuditEvents
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancel);

    public async Task AddAsync(AuditEvent auditEvent, CancellationToken cancel)
    {
        db.AuditEvents.Add(auditEvent);
        await db.SaveChangesAsync(cancel);
    }

    public Task SaveChangesAsync(CancellationToken cancel) =>
        db.SaveChangesAsync(cancel);
}
