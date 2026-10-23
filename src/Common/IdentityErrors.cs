namespace TrustSign.Common;

/// <summary>Danh mục lỗi nghiệp vụ của module Identity. Thêm lỗi mới ở đây để mã lỗi không bị trùng.</summary>
public static class IdentityErrors
{
    public static readonly Error SessionInvalid =
        Error.Unauthorized("Auth.SessionInvalid", "Phiên đăng nhập không còn hiệu lực.");
}
