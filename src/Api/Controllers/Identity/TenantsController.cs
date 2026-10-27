using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSign.Api.Errors;
using TrustSign.Api.Security;
using TrustSign.Application.Modules.Identity;
using TrustSign.Common;

namespace TrustSign.Api.Controllers.Identity;

[ApiController]
[Route("api/tenants")]
public sealed class TenantsController(ITenantService tenants) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequestDto request, CancellationToken cancel)
    {
        if (User.GetUserId() is not { } userId || User.GetSessionId() is not { } sessionId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        Result<TenantSessionResponseDto> result = await tenants.CreateTenantAsync(userId, sessionId, request, cancel);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken cancel)
    {
        if (User.GetUserId() is not { } userId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        Result<IReadOnlyList<TenantDto>> result = await tenants.GetMyTenantsAsync(userId, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpPost("{tenantId:guid}/switch")]
    public async Task<IActionResult> SwitchTenant(Guid tenantId, CancellationToken cancel)
    {
        if (User.GetUserId() is not { } userId || User.GetSessionId() is not { } sessionId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        Result<AuthResponseDto> result = await tenants.SwitchTenantAsync(userId, sessionId, tenantId, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.TenantMember)]
    [HttpGet("current/members")]
    public async Task<IActionResult> GetMembers(CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<IReadOnlyList<TenantMemberDto>> result = await tenants.GetMembersAsync(tenantId, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.TenantOwnerOnly)]
    [HttpPatch("current/members/{memberUserId:guid}")]
    public async Task<IActionResult> ChangeMemberRole(
        Guid memberUserId,
        [FromBody] ChangeMemberRoleRequestDto request,
        CancellationToken cancel)
    {
        if (User.GetUserId() is not { } actorId || User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<TenantMemberDto> result = await tenants.ChangeMemberRoleAsync(
            tenantId,
            actorId,
            memberUserId,
            request.Role,
            cancel);

        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.TenantOwnerOnly)]
    [HttpDelete("current/members/{memberUserId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid memberUserId, CancellationToken cancel)
    {
        if (User.GetUserId() is not { } actorId || User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result result = await tenants.RemoveMemberAsync(tenantId, actorId, memberUserId, cancel);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.TenantOwnerOnly)]
    [HttpPost("current/invitations")]
    public async Task<IActionResult> InviteMember(
        [FromBody] CreateInvitationRequestDto request,
        CancellationToken cancel)
    {
        if (User.GetUserId() is not { } inviterId || User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<TenantInvitationDto> result = await tenants.InviteMemberAsync(tenantId, inviterId, request, cancel);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.TenantOwnerOnly)]
    [HttpGet("current/invitations")]
    public async Task<IActionResult> GetPendingInvitations(CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<IReadOnlyList<TenantInvitationDto>> result = await tenants.GetPendingInvitationsAsync(tenantId, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.TenantOwnerOnly)]
    [HttpDelete("current/invitations/{invitationId:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid invitationId, CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result result = await tenants.RevokeInvitationAsync(tenantId, invitationId, cancel);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }
}
