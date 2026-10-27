using System.Security.Claims;
using TrustSign.Common;
using TrustSign.Common.Enums;

namespace TrustSign.Api.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Mã người dùng lấy từ claim <c>sub</c> của token đã kiểm tra; null nếu chưa đăng nhập.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtClaims.Subject), out Guid id) ? id : null;

    public static Guid? GetSessionId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtClaims.Session), out Guid id) ? id : null;

    public static Guid? GetTenantId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtClaims.TenantId), out Guid id) ? id : null;

    public static TenantRole? GetTenantRole(this ClaimsPrincipal principal) =>
        Enum.TryParse(principal.FindFirstValue(JwtClaims.TenantRole), out TenantRole role) ? role : null;

    public static TenantStatus? GetTenantStatus(this ClaimsPrincipal principal) =>
        Enum.TryParse(principal.FindFirstValue(JwtClaims.TenantStatus), out TenantStatus status) ? status : null;
}
