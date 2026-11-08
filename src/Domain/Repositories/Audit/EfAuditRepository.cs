using Microsoft.EntityFrameworkCore;
using TrustSign.Domain.Entities.Audit;
using TrustSign.Domain.IRepositories.Audit;

namespace TrustSign.Domain.Repositories.Audit;

internal sealed class EfAuditRepository(TrustSignDbContext db) : IAuditRepository
{
    // Tìm theo liên kết thay vì CreatedAt: hai sự kiện có thể trùng mili giây (ký lượt cuối + hoàn tất).
    public Task<AuditEvent?> GetChainTipAsync(string resourceType, string resourceId, CancellationToken cancel) =>
        db.AuditEvents
            .Where(e => e.ResourceType == resourceType && e.ResourceId == resourceId)
            .Where(e => !db.AuditEvents.Any(next =>
                next.ResourceType == resourceType && next.ResourceId == resourceId && next.PrevHash == e.Hash))
            .FirstOrDefaultAsync(cancel);

    public async Task<IReadOnlyList<AuditEvent>> ListForResourceAsync(
        Guid? tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancel) =>
        await db.AuditEvents
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.ResourceType == resourceType && e.ResourceId == resourceId)
            .ToListAsync(cancel);

    public async Task AddAsync(AuditEvent auditEvent, CancellationToken cancel)
    {
        db.AuditEvents.Add(auditEvent);
        await db.SaveChangesAsync(cancel);
    }

    public Task SaveChangesAsync(CancellationToken cancel) =>
        db.SaveChangesAsync(cancel);
}
