using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSign.Api.Errors;
using TrustSign.Api.Security;
using TrustSign.Application.Modules.Identity;
using TrustSign.Common;

namespace TrustSign.Api.Controllers.Identity;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IUserService users) : ControllerBase
{
    // TODO (SRS mục 5): POST register, verify-email, resend-verification, login, refresh, logout.

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancel)
    {
        if (User.GetUserId() is not { } userId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        Result<UserProfileDto> result = await users.GetProfileAsync(userId, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }
}
