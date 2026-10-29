using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSign.Api.Errors;
using TrustSign.Application.Modules.Subscriptions;
using TrustSign.Common;

namespace TrustSign.Api.Controllers.Subscriptions;

[ApiController]
[Route("api/subscription/admin/plans")]
[Authorize(Roles = "Admin")]
public sealed class AdminPlansController(IPlanService planService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllPlans(CancellationToken cancel)
    {
        Result<IReadOnlyList<PlanDto>> result = await planService.GetPlansAsync(includeInactive: true, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePlan([FromBody] SavePlanRequestDto request, CancellationToken cancel)
    {
        Result<PlanDto> result = await planService.CreatePlanAsync(request, cancel);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : this.ToProblem(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] SavePlanRequestDto request, CancellationToken cancel)
    {
        Result<PlanDto> result = await planService.UpdatePlanAsync(id, request, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }
}
