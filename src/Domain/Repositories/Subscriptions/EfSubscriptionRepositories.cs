using Microsoft.EntityFrameworkCore;
using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Subscriptions;
using TrustSign.Domain.IRepositories.Subscriptions;

namespace TrustSign.Domain.Repositories.Subscriptions;

internal sealed class EfPlanRepository(TrustSignDbContext db) : IPlanRepository
{
    public async Task<IReadOnlyList<Plan>> ListAsync(bool includeInactive, CancellationToken cancel) =>
        await db.Plans
            .Where(plan => includeInactive || plan.IsActive)
            .OrderByDescending(plan => plan.IsTrial)
            .ThenBy(plan => plan.Price)
            .ToListAsync(cancel);

    public Task<Plan?> FindByIdAsync(Guid planId, CancellationToken cancel) =>
        db.Plans.FirstOrDefaultAsync(plan => plan.Id == planId, cancel);

    public async Task<IReadOnlyDictionary<Guid, Plan>> FindByIdsAsync(IReadOnlyCollection<Guid> planIds, CancellationToken cancel) =>
        planIds.Count == 0
            ? new Dictionary<Guid, Plan>()
            : await db.Plans.Where(plan => planIds.Contains(plan.Id)).ToDictionaryAsync(plan => plan.Id, cancel);

    public Task<Plan?> FindActiveTrialAsync(CancellationToken cancel) =>
        db.Plans
            .Where(plan => plan.IsTrial && plan.IsActive)
            .OrderBy(plan => plan.CreatedAt)
            .FirstOrDefaultAsync(cancel);

    public Task<bool> AnyAsync(CancellationToken cancel) => db.Plans.AnyAsync(cancel);

    public async Task AddAsync(Plan plan, CancellationToken cancel)
    {
        db.Plans.Add(plan);
        await db.SaveChangesAsync(cancel);
    }

    public Task SaveChangesAsync(CancellationToken cancel) => db.SaveChangesAsync(cancel);
}

internal sealed class EfSubscriptionRepository(TrustSignDbContext db) : ISubscriptionRepository
{
    public Task<Subscription?> FindByTenantIdAsync(Guid tenantId, CancellationToken cancel) =>
        db.Subscriptions.FirstOrDefaultAsync(sub => sub.TenantId == tenantId, cancel);

    public async Task AddAsync(Subscription subscription, CancellationToken cancel)
    {
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(cancel);
    }

    public async Task<bool> TryConsumeQuotaAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancel)
    {
        // Sang kỳ mới (đã qua một tháng) thì đặt lại số đã dùng. Điều kiện nằm trong WHERE nên hai request cùng lúc
        // chỉ một câu lệnh đặt lại được; câu còn lại thấy quota_period_start đã đổi và bỏ qua.
        DateTimeOffset periodThreshold = now.AddMonths(-1);
        await db.Subscriptions
            .Where(sub => sub.TenantId == tenantId && sub.QuotaPeriodStart <= periodThreshold)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(sub => sub.QuotaUsed, 0)
                    .SetProperty(sub => sub.QuotaPeriodStart, now)
                    .SetProperty(sub => sub.UpdatedAt, now),
                cancel);

        // Trừ lượt nguyên tử: PostgreSQL khóa dòng khi UPDATE, request thứ hai chờ rồi đánh giá lại WHERE
        // với giá trị mới nên không bao giờ vượt quota_limit (TC21).
        int affected = await db.Subscriptions
            .Where(sub => sub.TenantId == tenantId
                && sub.Status != SubscriptionStatus.Expired
                && sub.ExpiresAt > now
                && sub.QuotaUsed < sub.QuotaLimit)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(sub => sub.QuotaUsed, sub => sub.QuotaUsed + 1)
                    .SetProperty(sub => sub.UpdatedAt, now),
                cancel);

        return affected == 1;
    }

    public Task<int> ExpireDueAsync(DateTimeOffset now, CancellationToken cancel) =>
        db.Subscriptions
            .Where(sub => sub.Status != SubscriptionStatus.Expired && sub.ExpiresAt <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(sub => sub.Status, SubscriptionStatus.Expired)
                    .SetProperty(sub => sub.UpdatedAt, now),
                cancel);

    public Task SaveChangesAsync(CancellationToken cancel) => db.SaveChangesAsync(cancel);
}

internal sealed class EfInvoiceRepository(TrustSignDbContext db) : IInvoiceRepository
{
    public Task<Invoice?> FindByCodeAsync(string code, CancellationToken cancel) =>
        db.Invoices.FirstOrDefaultAsync(invoice => invoice.Code == code, cancel);

    public async Task<IReadOnlyList<Invoice>> ListByTenantAsync(Guid tenantId, CancellationToken cancel) =>
        await db.Invoices
            .Where(invoice => invoice.TenantId == tenantId)
            .OrderByDescending(invoice => invoice.CreatedAt)
            .ToListAsync(cancel);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancel) =>
        db.Invoices.AnyAsync(invoice => invoice.Code == code, cancel);

    public async Task AddAsync(Invoice invoice, CancellationToken cancel)
    {
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancel);
    }

    public Task<int> ExpireDueAsync(DateTimeOffset now, CancellationToken cancel) =>
        db.Invoices
            .Where(invoice => invoice.Status == InvoiceStatus.Pending && invoice.ExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(invoice => invoice.Status, InvoiceStatus.Expired), cancel);

    public Task<bool> TransactionExistsAsync(long sepayTransactionId, CancellationToken cancel) =>
        db.PaymentTransactions.AnyAsync(tx => tx.SepayTransactionId == sepayTransactionId, cancel);

    public Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancel)
    {
        db.PaymentTransactions.Add(transaction);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancel) => db.SaveChangesAsync(cancel);
}
