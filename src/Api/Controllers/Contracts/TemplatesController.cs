using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSign.Api.Errors;
using TrustSign.Api.Security;
using TrustSign.Application.Modules.Contracts;
using TrustSign.Application.Modules.Identity;
using TrustSign.Common;

namespace TrustSign.Api.Controllers.Contracts;

[ApiController]
[Route("api/templates")]
[Authorize(Policy = AuthorizationPolicies.TenantMember)]
public sealed class TemplatesController(ITemplateService templateService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<IReadOnlyList<TemplateSummaryDto>> result = await templateService.ListAsync(tenantId, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<TemplateDetailDto> result = await templateService.GetByIdAsync(tenantId, id, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveTemplateRequestDto request, CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        if (User.GetUserId() is not { } userId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        Result<TemplateSummaryDto> result = await templateService.CreateAsync(tenantId, userId, request, cancel);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.ToProblem(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveTemplateRequestDto request, CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<TemplateSummaryDto> result = await templateService.UpdateAsync(tenantId, id, request, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result result = await templateService.DeleteAsync(tenantId, id, cancel);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }
}
