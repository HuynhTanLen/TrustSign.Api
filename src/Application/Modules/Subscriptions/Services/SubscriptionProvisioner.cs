using Microsoft.Extensions.Logging;
using TrustSign.Common;
using TrustSign.Domain.Entities.Subscriptions;
using TrustSign.Domain.IRepositories.Subscriptions;

namespace TrustSign.Application.Modules.Subscriptions;

internal sealed class SubscriptionProvisioner(
    ISubscriptionRepository subscriptions,
    IPlanRepository plans,
    IPlanSeeder seeder,
    ILogger<SubscriptionProvisioner> logger) : ISubscriptionProvisioner
{
    public async Task<Result> ProvisionTrialSubscriptionAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancel = default)
    {
        Subscription? existing = await subscriptions.FindByTenantIdAsync(tenantId, cancel);
        if (existing is not null)
        {
            return Result.Success();
        }

        Plan? trialPlan = await plans.FindActiveTrialAsync(cancel);
        if (trialPlan is null)
        {
            await seeder.SeedDefaultPlansAsync(cancel);
            trialPlan = await plans.FindActiveTrialAsync(cancel);
            if (trialPlan is null)
            {
                logger.LogError("Không tìm thấy gói dùng thử để cấp cho doanh nghiệp {TenantId}", tenantId);
                return SubscriptionErrors.PlanNotFound;
            }
        }

        Subscription trialSubscription = Subscription.StartTrial(tenantId, trialPlan, now);
        await subscriptions.AddAsync(trialSubscription, cancel);
        await subscriptions.SaveChangesAsync(cancel);

        logger.LogInformation(
            "Đã cấp gói dùng thử ({Days} ngày, {Quota} lượt ký) cho doanh nghiệp {TenantId}",
            trialPlan.DurationDays,
            trialPlan.MonthlyQuota,
            tenantId);

        return Result.Success();
    }
}
