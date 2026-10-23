using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using TrustSign.Application.Modules.Identity;

namespace TrustSign.Api.Controllers.Identity;

/// <summary>
/// Công bố public key ký JWT (NFR10). Ứng dụng tự kiểm token bằng bộ khóa trong bộ nhớ nên không cần đọc trang này;
/// nó dành cho bên ngoài muốn tự kiểm token, và cho một module được tách thành service riêng sau này.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class JwksController(SigningKeyRing keyRing) : ControllerBase
{
    [HttpGet("/.well-known/jwks.json")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public IActionResult Get() => Ok(new
    {
        // Chỉ xuất n và e; không bao giờ xuất phần private.
        keys = keyRing.PublicKeys
            .Select(JsonWebKeyConverter.ConvertFromRSASecurityKey)
            .Select(jwk => new { kty = jwk.Kty, kid = jwk.Kid, use = "sig", alg = SecurityAlgorithms.RsaSha256, n = jwk.N, e = jwk.E })
    });
}
