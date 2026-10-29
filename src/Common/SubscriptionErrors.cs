namespace TrustSign.Common;

/// <summary>Danh mục lỗi nghiệp vụ của module Subscriptions (F17–F20).</summary>
public static class SubscriptionErrors
{
    public static readonly Error PlanNotFound =
        Error.NotFound("Subscription.PlanNotFound", "Không tìm thấy gói dịch vụ.");

    public static readonly Error InvalidPlan =
        Error.Validation("Subscription.InvalidPlan", "Thông tin gói dịch vụ không hợp lệ (tên 1–100 ký tự, giá ≥ 0, số ngày và hạn mức > 0).");

    public static readonly Error PlanNotPurchasable =
        Error.Validation("Subscription.PlanNotPurchasable", "Gói này không thể mua (gói dùng thử hoặc đã ngừng bán).");

    public static readonly Error SubscriptionNotFound =
        Error.NotFound("Subscription.NotFound", "Doanh nghiệp chưa có thuê bao.");

    public static readonly Error SubscriptionExpired =
        Error.Forbidden("Subscription.Expired", "Thuê bao đã hết hạn. Vui lòng gia hạn hoặc nâng gói.");

    public static readonly Error QuotaExceeded =
        Error.Forbidden("Subscription.QuotaExceeded", "Đã dùng hết hạn mức hợp đồng gửi ký trong tháng. Vui lòng nâng gói.");

    public static readonly Error InvoiceNotFound =
        Error.NotFound("Subscription.InvoiceNotFound", "Không tìm thấy hóa đơn.");

    public static readonly Error InvoiceNotPaid =
        Error.Validation("Subscription.InvoiceNotPaid", "Hóa đơn chưa được thanh toán nên chưa xuất được biên nhận.");

    public static readonly Error WebhookUnauthorized =
        Error.Unauthorized("Subscription.WebhookUnauthorized", "API key của webhook SePay không hợp lệ.");
}
