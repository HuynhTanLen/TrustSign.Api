using TrustSign.Common;

namespace TrustSign.Application.Modules.Subscriptions;

public interface IPlanService
{
    Task<Result<IReadOnlyList<PlanDto>>> GetPlansAsync(bool includeInactive, CancellationToken cancel = default);

    Task<Result<PlanDto>> CreatePlanAsync(SavePlanRequestDto request, CancellationToken cancel = default);

    Task<Result<PlanDto>> UpdatePlanAsync(Guid id, SavePlanRequestDto request, CancellationToken cancel = default);
}
