namespace TrustSign.Common.Enums;

/// <summary>
/// Vai trò của người nhận trong quy trình luân chuyển hợp đồng (F03, F04):
/// - Signer: Người ký chính, cần vị trí ô ký, xác thực qua OTP và ký số PAdES.
/// - Approver: Người duyệt theo thứ tự (qua OTP), không đóng chữ ký lên tài liệu và không cần ô ký.
/// - Cc: Người nhận bản sao theo dõi, không tham gia luồng ký, chỉ nhận email kèm link xem khi hợp đồng hoàn tất.
/// - Coordinator: Đầu mối bên đối tác. Tới lượt thì tự khai người duyệt/ký của bên mình; người ký đó nhận ô ký
///   bên tạo đã đặt sẵn cho người điều phối (như "Người điều phối" của FPT.eContract, "Coordinate" của VNPT eContract).
/// </summary>
public enum RecipientRole
{
    Signer = 0,
    Approver = 1,
    Cc = 2,
    Coordinator = 3
}
