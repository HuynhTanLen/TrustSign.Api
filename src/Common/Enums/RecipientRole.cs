namespace TrustSign.Common.Enums;

/// <summary>
/// Vai trò của người nhận trong quy trình luân chuyển hợp đồng (F03, F04):
/// - Signer: Người ký chính, cần vị trí ô ký, xác thực qua OTP và ký số PAdES.
/// - Approver: Người duyệt theo thứ tự (qua OTP), không đóng chữ ký lên tài liệu và không cần ô ký.
/// - Cc: Người nhận bản sao theo dõi, không tham gia luồng ký, chỉ nhận email kèm link xem khi hợp đồng hoàn tất.
/// </summary>
public enum RecipientRole
{
    Signer = 0,
    Approver = 1,
    Cc = 2
}
