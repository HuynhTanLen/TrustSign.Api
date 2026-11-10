using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Domain.Entities.Subscriptions;
using TrustSign.Domain.IRepositories.Subscriptions;

namespace TrustSign.Application.Modules.Subscriptions;

internal sealed class QuotaService(
    ISubscriptionRepository subscriptions,
    ISubscriptionProvisioner subscriptionProvisioner,
    TimeProvider timeProvider) : IQuotaService
{
    public async Task<bool> HasAvailableQuotaAsync(Guid tenantId, CancellationToken cancel = default)
    {
        Subscription? sub = await subscriptions.FindByTenantIdAsync(tenantId, cancel);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (sub is null)
        {
            try
            {
                await subscriptionProvisioner.ProvisionTrialSubscriptionAsync(tenantId, now, cancel);
            }
            catch
            {
                // Bỏ qua race condition
            }
            sub = await subscriptions.FindByTenantIdAsync(tenantId, cancel);
        }

        if (sub is null)
        {
            return false;
        }

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
            try
            {
                await subscriptionProvisioner.ProvisionTrialSubscriptionAsync(tenantId, now, cancel);
            }
            catch
            {
                // Bỏ qua race condition
            }

            consumed = await subscriptions.TryConsumeQuotaAsync(tenantId, now, cancel);
            if (consumed)
            {
                return Result.Success();
            }

            sub = await subscriptions.FindByTenantIdAsync(tenantId, cancel);
        }

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
