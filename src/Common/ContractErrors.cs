namespace TrustSign.Common;

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

    public static readonly Error AtLeastOneSignerRequired =
        Error.Validation("Contract.NoSigner", "Hợp đồng phải có ít nhất một người ký (Signer).");

    public static readonly Error InvalidSignatureBox =
        Error.Validation("Contract.InvalidSignatureBox", "Tọa độ hoặc kích thước ô chữ ký trên trang PDF không hợp lệ.");

    public static readonly Error InvalidDueDate =
        Error.Validation("Contract.InvalidDueDate", "Thời hạn ký phải lớn hơn thời điểm hiện tại.");

    public static readonly Error ContractCannotCancel =
        Error.Validation("Contract.CannotCancel", "Chỉ có thể hủy hợp đồng khi đang ở trạng thái Chờ ký hoặc Đang ký.");

    public static readonly Error DocumentAccessDenied =
        Error.Unauthorized("Contract.DocumentAccessDenied", "Liên kết xem tài liệu không hợp lệ hoặc đã hết hạn.");

    public static readonly Error SignerNotFound =
        Error.NotFound("Contract.SignerNotFound", "Không tìm thấy người ký trong hợp đồng.");

    public static readonly Error NotSignerTurn =
        Error.Validation("Contract.NotSignerTurn", "Chưa tới lượt ký hoặc duyệt của người này.");

    public static readonly Error CcCannotSignOrApprove =
        Error.Validation("Contract.CcCannotSignOrApprove", "Người nhận bản sao (Cc) không tham gia ký hoặc duyệt.");
}
