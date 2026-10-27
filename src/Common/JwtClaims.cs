namespace TrustSign.Common;

/// <summary>Tên claim trong access token (NFR17). Mọi chỗ đọc danh tính người gọi đều dùng các tên này.</summary>
public static class JwtClaims
{
    public const string Subject = "sub";
    public const string Session = "sid";
    public const string Role = "role";
    public const string TenantId = "tenant_id";
    public const string TenantRole = "tenant_role";
    public const string TenantStatus = "tenant_status";
}
