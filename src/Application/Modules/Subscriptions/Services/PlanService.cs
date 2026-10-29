using TrustSign.Common;
using TrustSign.Domain.Entities.Subscriptions;
using TrustSign.Domain.IRepositories.Subscriptions;

namespace TrustSign.Application.Modules.Subscriptions;

internal sealed class PlanService(
    IPlanRepository plans,
    TimeProvider timeProvider) : IPlanService
{
    public async Task<Result<IReadOnlyList<PlanDto>>> GetPlansAsync(bool includeInactive, CancellationToken cancel = default)
    {
        IReadOnlyList<Plan> planEntities = await plans.ListAsync(includeInactive, cancel);
        return planEntities.Select(ToDto).ToList();
    }

    public async Task<Result<PlanDto>> CreatePlanAsync(SavePlanRequestDto request, CancellationToken cancel = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Result<Plan> createResult = Plan.Create(
            request.Name,
            request.Price,
            request.DurationDays,
            request.MonthlyQuota,
            request.IsTrial,
            request.IsActive,
            now);

        if (createResult.IsFailure)
        {
            return createResult.Error;
        }

        Plan plan = createResult.Value;
        await plans.AddAsync(plan, cancel);
        return ToDto(plan);
    }

    public async Task<Result<PlanDto>> UpdatePlanAsync(Guid id, SavePlanRequestDto request, CancellationToken cancel = default)
    {
        Plan? plan = await plans.FindByIdAsync(id, cancel);
        if (plan is null)
        {
            return SubscriptionErrors.PlanNotFound;
        }

        Result updateResult = plan.Update(
            request.Name,
            request.Price,
            request.DurationDays,
            request.MonthlyQuota,
            request.IsTrial,
            request.IsActive);

        if (updateResult.IsFailure)
        {
            return updateResult.Error;
        }

        await plans.SaveChangesAsync(cancel);
        return ToDto(plan);
    }

    private static PlanDto ToDto(Plan p) => new(
        p.Id,
        p.Name,
        p.Price,
        p.DurationDays,
        p.MonthlyQuota,
        p.IsTrial,
        p.IsActive);
}
