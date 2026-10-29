using TrustSign.Domain.Entities.Subscriptions;

namespace TrustSign.Domain.IRepositories.Subscriptions;

public interface ISubscriptionRepository
{
    Task<Subscription?> FindByTenantIdAsync(Guid tenantId, CancellationToken cancel);

    Task AddAsync(Subscription subscription, CancellationToken cancel);

    /// <summary>
    /// Trừ một lượt hạn mức bằng câu lệnh cập nhật có điều kiện (nguyên tử ở mức database):
    /// <c>UPDATE … SET quota_used = quota_used + 1 WHERE tenant_id = @t AND quota_used &lt; quota_limit AND expires_at &gt; @now</c>.
    /// Trả true nếu trừ được (TC15, TC21).
    /// </summary>
    Task<bool> TryConsumeQuotaAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancel);

    /// <summary>Chuyển mọi thuê bao quá hạn sang Hết hạn, trả số bản ghi đã đổi (TC18).</summary>
    Task<int> ExpireDueAsync(DateTimeOffset now, CancellationToken cancel);

    Task SaveChangesAsync(CancellationToken cancel);
}
