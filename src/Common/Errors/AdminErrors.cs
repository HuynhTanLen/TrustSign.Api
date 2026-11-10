namespace TrustSign.Common.Errors;

/// <summary>Lỗi của khu quản trị nền tảng (F14, ADM-01).</summary>
public static class AdminErrors
{
    public const int MaxLockReasonLength = 500;

    public static readonly Error TenantNotFound =
        Error.NotFound("Admin.TenantNotFound", "Không tìm thấy doanh nghiệp.");

    public static readonly Error UserNotFound =
        Error.NotFound("Admin.UserNotFound", "Không tìm thấy người dùng.");

    public static readonly Error LockReasonRequired =
        Error.Validation("Admin.LockReasonRequired", "Vui lòng nhập lý do khóa (tối đa 500 ký tự).");

    public static readonly Error CannotLockAdmin =
        Error.Forbidden("Admin.CannotLockAdmin", "Không thể khóa tài khoản quản trị viên.");

    public static readonly Error UserNotActive =
        Error.Conflict("Admin.UserNotActive", "Tài khoản chưa xác minh email nên chưa đăng nhập được; không cần khóa.");

    public static readonly Error AlreadyLocked =
        Error.Conflict("Admin.AlreadyLocked", "Đối tượng này đang bị khóa.");

    public static readonly Error NotLocked =
        Error.Conflict("Admin.NotLocked", "Đối tượng này không bị khóa.");
}
