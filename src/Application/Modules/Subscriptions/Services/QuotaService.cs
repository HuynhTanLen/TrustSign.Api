using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Subscriptions;
using TrustSign.Domain.IRepositories.Subscriptions;

namespace TrustSign.Application.Modules.Subscriptions;

internal sealed class QuotaService(
    ISubscriptionRepository subscriptions,
    TimeProvider timeProvider) : IQuotaService
{
    public async Task<bool> HasAvailableQuotaAsync(Guid tenantId, CancellationToken cancel = default)
    {
        Subscription? sub = await subscriptions.FindByTenantIdAsync(tenantId, cancel);
        if (sub is null)
        {
            return false;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        return sub.EffectiveStatus(now) != SubscriptionStatus.Expired && sub.QuotaUsed < sub.QuotaLimit;
    }

    public async Task<Result> ConsumeQuotaAsync(Guid tenantId, CancellationToken cancel = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        bool consumed = await subscriptions.TryConsumeQuotaAsync(tenantId, now, cancel);
        if (consumed)
        {
            return Result.Success();
        }

        Subscription? sub = await subscriptions.FindByTenantIdAsync(tenantId, cancel);
        if (sub is null)
        {
            return SubscriptionErrors.SubscriptionNotFound;
        }

        if (sub.EffectiveStatus(now) == SubscriptionStatus.Expired)
        {
            return SubscriptionErrors.SubscriptionExpired;
        }

        return SubscriptionErrors.QuotaExceeded;
    }
}
