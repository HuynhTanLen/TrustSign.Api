using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSign.Api.Errors;
using TrustSign.Api.Security;
using TrustSign.Application.Modules.Identity;
using TrustSign.Common;

namespace TrustSign.Api.Controllers.Identity;

[ApiController]
[Route("api/invitations")]
public sealed class InvitationsController(ITenantService tenants) : ControllerBase
{
    [Authorize]
    [HttpPost("{token}/accept")]
    public async Task<IActionResult> AcceptInvitation(string token, CancellationToken cancel)
    {
        if (User.GetUserId() is not { } userId || User.GetSessionId() is not { } sessionId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        Result<TenantSessionResponseDto> result = await tenants.AcceptInvitationAsync(userId, sessionId, token, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }
}
