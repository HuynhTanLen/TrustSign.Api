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

    public static readonly Error AccountDisabled =
        Error.Forbidden("Auth.AccountDisabled", "Tài khoản đã bị khóa bởi quản trị viên.");

    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "Email hoặc mật khẩu không đúng.");

    public static readonly Error EmailAlreadyExists =
        Error.Conflict("Auth.EmailAlreadyExists", "Địa chỉ email này đã được đăng ký.");

    public static readonly Error InvalidEmail =
        Error.Validation("Auth.InvalidEmail", "Địa chỉ email không hợp lệ.");

    public static readonly Error InvalidFullName =
        Error.Validation("Auth.InvalidFullName", "Họ và tên là bắt buộc và tối đa 100 ký tự.");

    public static readonly Error WeakPassword =
        Error.Validation("Auth.WeakPassword", "Mật khẩu phải có ít nhất 8 ký tự.");

    public static readonly Error VerificationTokenInvalid =
        Error.Validation("Auth.VerificationTokenInvalid", "Liên kết xác minh không hợp lệ hoặc đã hết hạn.");

    public static readonly Error RefreshTokenInvalid =
        Error.Unauthorized("Auth.RefreshTokenInvalid", "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    public static readonly Error RefreshTokenReused =
        Error.Unauthorized("Auth.RefreshTokenReused", "Phát hiện sử dụng lại refresh token cũ. Phiên đăng nhập đã bị thu hồi.");

    public static readonly Error InvalidTenantName =
        Error.Validation("Tenant.InvalidName", "Tên doanh nghiệp là bắt buộc và tối đa 200 ký tự.");

    public static readonly Error TenantNotFound =
        Error.NotFound("Tenant.NotFound", "Không tìm thấy doanh nghiệp.");

    public static readonly Error TenantAccessDenied =
        Error.Forbidden("Tenant.AccessDenied", "Bạn không có quyền thực hiện thao tác này trong doanh nghiệp.");

    public static readonly Error TenantLocked =
        Error.Forbidden("Tenant.Locked", "Doanh nghiệp đang bị tạm khóa.");

    public static readonly Error MemberNotFound =
        Error.NotFound("Tenant.MemberNotFound", "Không tìm thấy thành viên trong doanh nghiệp.");

    public static readonly Error AlreadyMember =
        Error.Conflict("Tenant.AlreadyMember", "Người dùng này đã là thành viên của doanh nghiệp.");

    public static readonly Error CannotRemoveSelf =
        Error.Validation("Tenant.CannotRemoveSelf", "Không thể tự xóa chính mình khỏi doanh nghiệp.");

    public static readonly Error CannotRemoveLastOwner =
        Error.Validation("Tenant.CannotRemoveLastOwner", "Không thể xóa hoặc hạ quyền Chủ doanh nghiệp cuối cùng.");

    public static readonly Error InvitationNotFound =
        Error.NotFound("Tenant.InvitationNotFound", "Không tìm thấy lời mời hoặc lời mời không còn hiệu lực.");

    public static readonly Error InvitationEmailMismatch =
        Error.Forbidden("Tenant.InvitationEmailMismatch", "Lời mời này được gửi cho một địa chỉ email khác.");
}
