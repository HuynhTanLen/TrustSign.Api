namespace TrustSign.Common.Enums;

/// <summary>Vòng đời hợp đồng (F05–F10): Nháp → Chờ ký → Đang ký → Hoàn tất / Từ chối / Đã hủy / Hết hạn.</summary>
public enum ContractStatus
{
    Draft = 0,
    Pending = 1,
    Signing = 2,
    Completed = 3,
    Declined = 4,
    Cancelled = 5,
    Expired = 6
}
