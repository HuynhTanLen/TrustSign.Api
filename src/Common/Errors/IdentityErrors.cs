namespace TrustSign.Common.Errors;

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

    public static readonly Error TermsMustBeAccepted =
        Error.Validation("Auth.TermsMustBeAccepted", "Bạn cần đồng ý với Điều khoản sử dụng và Chính sách bảo vệ dữ liệu cá nhân (NĐ 13/2023) để đăng ký tài khoản.");

    public static readonly Error InvalidPhoneNumber =
        Error.Validation("Auth.InvalidPhoneNumber", "Số điện thoại không đúng định dạng Việt Nam (bắt đầu bằng 0 hoặc +84 và theo sau là 9 chữ số).");

    public static readonly Error InvalidJobTitle =
        Error.Validation("Auth.InvalidJobTitle", "Chức danh / chức vụ tối đa 100 ký tự.");

    public static readonly Error VerificationTokenInvalid =
        Error.Validation("Auth.VerificationTokenInvalid", "Liên kết xác minh không hợp lệ hoặc đã hết hạn.");

    public static readonly Error PasswordResetTokenInvalid =
        Error.Validation("Auth.PasswordResetTokenInvalid", "Liên kết đặt lại mật khẩu không hợp lệ, đã hết hạn hoặc đã được sử dụng.");

    public static readonly Error CurrentPasswordIncorrect =
        Error.Validation("Auth.CurrentPasswordIncorrect", "Mật khẩu hiện tại không đúng.");

    public static readonly Error PasswordUnchanged =
        Error.Validation("Auth.PasswordUnchanged", "Mật khẩu mới phải khác mật khẩu hiện tại.");

    public static readonly Error RefreshTokenInvalid =
        Error.Unauthorized("Auth.RefreshTokenInvalid", "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    public static readonly Error RefreshTokenReused =
        Error.Unauthorized("Auth.RefreshTokenReused", "Phát hiện sử dụng lại refresh token cũ. Phiên đăng nhập đã bị thu hồi.");

    public static readonly Error InvalidTenantName =
        Error.Validation("Tenant.InvalidName", "Tên doanh nghiệp là bắt buộc và tối đa 200 ký tự.");

    public static readonly Error InvalidTaxCode =
        Error.Validation("Tenant.InvalidTaxCode", "Mã số thuế không hợp lệ (phải gồm 10 chữ số hoặc 13 chữ số có dấu gạch nối, ví dụ: 0101234567 hoặc 0101234567-001).");

    public static readonly Error InvalidRepresentativeName =
        Error.Validation("Tenant.InvalidRepresentativeName", "Tên người đại diện tối đa 100 ký tự.");

    public static readonly Error InvalidRepresentativeTitle =
        Error.Validation("Tenant.InvalidRepresentativeTitle", "Chức vụ người đại diện tối đa 100 ký tự.");

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

    public static readonly Error EkycInvalidQr =
        Error.Validation("Ekyc.InvalidQr", "Không đọc được mã QR trên CCCD. Hãy chụp lại mặt trước rõ nét, đủ sáng.");

    public static readonly Error EkycInvalidImage =
        Error.Validation("Ekyc.InvalidImage", "Ảnh phải là JPEG hoặc PNG và không quá 5 MB.");

    public static readonly Error EkycFaceMismatch =
        Error.Validation("Ekyc.FaceMismatch", "Khuôn mặt không khớp với ảnh trên CCCD.");

    public static readonly Error EkycAlreadyVerified =
        Error.Conflict("Ekyc.AlreadyVerified", "Tài khoản đã được xác minh danh tính.");

    public static readonly Error EkycCitizenIdTaken =
        Error.Conflict("Ekyc.CitizenIdTaken", "Số CCCD này đã được dùng để xác minh một tài khoản khác.");

    public static readonly Error EkycNotFound =
        Error.NotFound("Ekyc.NotFound", "Không tìm thấy hồ sơ xác minh.");

    // 404 thay vì 401 để trang điện thoại (không đăng nhập) không bị chuyển về màn hình đăng nhập.
    public static readonly Error EkycHandoffInvalid =
        Error.NotFound("Ekyc.HandoffInvalid", "Liên kết xác minh đã hết hạn hoặc không hợp lệ. Hãy quét lại mã QR trên máy tính.");

    public static readonly Error AvatarInvalid =
        Error.Validation("Profile.AvatarInvalid", "Ảnh đại diện phải là JPEG, PNG hoặc WebP và tối đa 128 KB.");

    public static readonly Error AvatarNotFound =
        Error.NotFound("Profile.AvatarNotFound", "Không tìm thấy ảnh đại diện.");

    public static readonly Error IdentityNotVerified =
        Error.Forbidden("Identity.NotVerified", "Bạn cần xác minh danh tính (eKYC) trước khi tạo hợp đồng.");

    public static readonly Error ExternalTokenInvalid =
        Error.Unauthorized("Auth.ExternalTokenInvalid", "Không xác thực được tài khoản Google. Vui lòng thử lại.");

    public static readonly Error GoogleEmailNotVerified =
        Error.Forbidden("Auth.GoogleEmailNotVerified", "Email của tài khoản Google chưa được xác minh.");

    /// <summary>Email Google trùng một tài khoản có sẵn: frontend hỏi mật khẩu rồi gửi lại cùng ID token.</summary>
    public static readonly Error GoogleLinkRequiresPassword =
        Error.Conflict("Auth.GoogleLinkRequiresPassword", "Email này đã có tài khoản. Nhập mật khẩu hiện tại để liên kết với Google.");

    public static readonly Error GoogleAlreadyLinked =
        Error.Conflict("Auth.GoogleAlreadyLinked", "Tài khoản Google này đã được liên kết với một tài khoản khác.");

    public static readonly Error GoogleNotLinked =
        Error.NotFound("Auth.GoogleNotLinked", "Tài khoản chưa liên kết Google.");

    public static readonly Error CannotUnlinkLastMethod =
        Error.Validation("Auth.CannotUnlinkLastMethod", "Hãy đặt mật khẩu (Quên mật khẩu) trước khi hủy liên kết Google, nếu không bạn sẽ không đăng nhập được.");

    public static readonly Error PasswordNotSet =
        Error.Unauthorized("Auth.PasswordNotSet", "Tài khoản này đăng nhập bằng Google. Hãy dùng Google, hoặc Quên mật khẩu để đặt mật khẩu.");

    public static readonly Error EkycNotVerified =
        Error.Validation("Ekyc.NotVerified", "Hồ sơ này không còn ở trạng thái đã xác minh.");
}
