using Microsoft.Extensions.Logging;
using TrustSign.Domain.Entities.Subscriptions;
using TrustSign.Domain.IRepositories.Subscriptions;

namespace TrustSign.Application.Modules.Subscriptions;

internal sealed class PlanSeeder(
    IPlanRepository plans,
    TimeProvider timeProvider,
    ILogger<PlanSeeder> logger) : IPlanSeeder
{
    public async Task SeedDefaultPlansAsync(CancellationToken cancel = default)
    {
        if (await plans.AnyAsync(cancel))
        {
            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        logger.LogInformation("Khởi tạo danh mục gói dịch vụ mặc định...");

        var trialPlanResult = Plan.Create("Dùng thử", 0, 14, 5, isTrial: true, isActive: true, now);
        var basicPlanResult = Plan.Create("Cơ bản", 199_000, 30, 30, isTrial: false, isActive: true, now);
        var enterprisePlanResult = Plan.Create("Doanh nghiệp", 599_000, 30, 150, isTrial: false, isActive: true, now);

        if (trialPlanResult.IsSuccess)
        {
            await plans.AddAsync(trialPlanResult.Value, cancel);
        }

        if (basicPlanResult.IsSuccess)
        {
            await plans.AddAsync(basicPlanResult.Value, cancel);
        }

        if (enterprisePlanResult.IsSuccess)
        {
            await plans.AddAsync(enterprisePlanResult.Value, cancel);
        }

        await plans.SaveChangesAsync(cancel);
        logger.LogInformation("Đã khởi tạo thành công 3 gói dịch vụ mặc định: Dùng thử, Cơ bản, Doanh nghiệp.");
    }
}
