using System.Security.Claims;
using TrustSign.Common;

namespace TrustSign.Api.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Mã người dùng lấy từ claim <c>sub</c> của token đã kiểm tra; null nếu chưa đăng nhập.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtClaims.Subject), out Guid id) ? id : null;
}
