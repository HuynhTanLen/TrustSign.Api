namespace TrustSign.Common;

/// <summary>Danh mục lỗi nghiệp vụ của module Identity. Thêm lỗi mới ở đây để mã lỗi không bị trùng.</summary>
public static class IdentityErrors
{
    public static readonly Error SessionInvalid =
        Error.Unauthorized("Auth.SessionInvalid", "Phiên đăng nhập không còn hiệu lực.");
    public static readonly Error AccountLocked =
    Error.TooManyRequests("Auth.AccountLocked", "Tài khoản bị khóa tạm thời do đăng nhập sai nhiều lần.");
    public static readonly Error EmailNotVerified =
    Error.Forbidden("Auth.EmailNotVerified", "Vui lòng xác minh email trước khi đăng nhập.");
}
