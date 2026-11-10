using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSign.Api.Http;
using TrustSign.Api.Security;
using TrustSign.Application.Modules.Identity;
using TrustSign.Common;
using TrustSign.Common.Errors;

namespace TrustSign.Api.Controllers.Identity;

/// <summary>Quản trị nền tảng (F14, ADM-01): chỉ tài khoản có vai trò Admin.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminController(IAdminService admin) : ControllerBase
{
    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants(CancellationToken cancel) =>
        Ok(await admin.ListTenantsAsync(cancel));

    [HttpGet("tenants/{id:guid}")]
    public async Task<IActionResult> GetTenant(Guid id, CancellationToken cancel) =>
        ToActionResult(await admin.GetTenantAsync(id, cancel));

    [HttpPost("tenants/{id:guid}/lock")]
    public Task<IActionResult> LockTenant(Guid id, [FromBody] AdminLockRequestDto request, CancellationToken cancel) =>
        AsAdminAsync(adminId => admin.LockTenantAsync(adminId, id, request.Reason, cancel));

    [HttpPost("tenants/{id:guid}/unlock")]
    public Task<IActionResult> UnlockTenant(Guid id, CancellationToken cancel) =>
        AsAdminAsync(adminId => admin.UnlockTenantAsync(adminId, id, cancel));

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(CancellationToken cancel) =>
        Ok(await admin.ListUsersAsync(cancel));

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken cancel) =>
        ToActionResult(await admin.GetUserAsync(id, cancel));

    [HttpPost("users/{id:guid}/lock")]
    public Task<IActionResult> LockUser(Guid id, [FromBody] AdminLockRequestDto request, CancellationToken cancel) =>
        AsAdminAsync(adminId => admin.LockUserAsync(adminId, id, request.Reason, cancel));

    [HttpPost("users/{id:guid}/unlock")]
    public Task<IActionResult> UnlockUser(Guid id, CancellationToken cancel) =>
        AsAdminAsync(adminId => admin.UnlockUserAsync(adminId, id, cancel));

    private IActionResult ToActionResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);

    private async Task<IActionResult> AsAdminAsync(Func<Guid, Task<Result>> action)
    {
        if (User.GetUserId() is not { } adminId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        Result result = await action(adminId);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }
}
