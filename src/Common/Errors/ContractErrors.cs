namespace TrustSign.Common.Errors;

/// <summary>Danh mục lỗi nghiệp vụ của module Contracts (F02–F05, F09, F21–F23).</summary>
public static class ContractErrors
{
    public static readonly Error TemplateNotFound =
        Error.NotFound("Contract.TemplateNotFound", "Không tìm thấy mẫu hợp đồng.");

    public static readonly Error InvalidTemplate =
        Error.Validation("Contract.InvalidTemplate", "Tên mẫu (1–200 ký tự) và nội dung mẫu là bắt buộc.");

    public static readonly Error MissingTemplateVariable =
        Error.Validation("Contract.MissingTemplateVariable", "Vui lòng điền đầy đủ tất cả các biến trong mẫu hợp đồng.");

    public static readonly Error ContractNotFound =
        Error.NotFound("Contract.NotFound", "Không tìm thấy hợp đồng.");

    public static readonly Error InvalidContractTitle =
        Error.Validation("Contract.InvalidTitle", "Tiêu đề hợp đồng là bắt buộc và tối đa 200 ký tự.");

    public static readonly Error InvalidPdfFile =
        Error.Validation("Contract.InvalidPdfFile", "Tệp tải lên phải là định dạng PDF hợp lệ và dung lượng tối đa 10 MB.");

    public static readonly Error ContractNotEditable =
        Error.Validation("Contract.NotEditable", "Chỉ có thể chỉnh sửa hợp đồng khi đang ở trạng thái Nháp.");

    public static readonly Error InvalidSigners =
        Error.Validation("Contract.InvalidSigners", "Hợp đồng phải có từ 1 đến 5 người nhận với đầy đủ họ tên và địa chỉ email hợp lệ.");

    public static Error InvalidSignerName(int order) =>
        Error.Validation("Contract.InvalidSignerName", $"Người nhận thứ {order}: họ tên phải từ 1 đến 100 ký tự.");

    public static Error InvalidSignerEmail(int order) =>
        Error.Validation("Contract.InvalidSignerEmail", $"Người nhận thứ {order}: địa chỉ email không hợp lệ.");

    public static readonly Error AtLeastOneSignerRequired =
        Error.Validation("Contract.NoSigner", "Hợp đồng phải có ít nhất một người ký hoặc người điều phối.");

    public static readonly Error InvalidSignatureBox =
        Error.Validation("Contract.InvalidSignatureBox", "Tọa độ hoặc kích thước ô chữ ký trên trang PDF không hợp lệ.");

    public static readonly Error InvalidDueDate =
        Error.Validation("Contract.InvalidDueDate", "Thời hạn ký phải lớn hơn thời điểm hiện tại.");

    public static readonly Error ContractCannotCancel =
        Error.Validation("Contract.CannotCancel", "Chỉ có thể hủy hợp đồng khi đang ở trạng thái Nháp, Chờ ký hoặc Đang ký.");

    public static readonly Error DocumentAccessDenied =
        Error.Unauthorized("Contract.DocumentAccessDenied", "Liên kết xem tài liệu không hợp lệ hoặc đã hết hạn.");

    public static readonly Error SignerNotFound =
        Error.NotFound("Contract.SignerNotFound", "Không tìm thấy người ký trong hợp đồng.");

    public static readonly Error NotSignerTurn =
        Error.Validation("Contract.NotSignerTurn", "Chưa tới lượt ký hoặc duyệt của người này.");

    public static readonly Error NoPendingSigner =
        Error.Validation("Contract.NoPendingSigner", "Hợp đồng không còn ai đang chờ ký hoặc duyệt.");

    public static readonly Error InvitationEmailFailed =
        Error.Validation("Contract.InvitationEmailFailed", "Không gửi được email mời ký. Kiểm tra cấu hình email rồi thử lại.");

    public static readonly Error CcCannotSignOrApprove =
        Error.Validation("Contract.CcCannotSignOrApprove", "Người nhận bản sao (Cc) không tham gia ký hoặc duyệt.");

    public static readonly Error CoordinatorMustCoordinate =
        Error.Validation("Contract.CoordinatorMustCoordinate", "Người điều phối không ký hay duyệt mà khai báo người duyệt/ký của bên mình.");

    public static readonly Error NotCoordinator =
        Error.Validation("Contract.NotCoordinator", "Người nhận này không có quyền điều phối.");

    public static readonly Error InvalidCoordination =
        Error.Validation("Contract.InvalidCoordination", "Cần khai báo đúng một người ký, có thể thêm người duyệt hoặc Cc; tổng số người nhận của hợp đồng tối đa 5.");
}
