using TrustSign.Common.Enums;

namespace TrustSign.Domain.Entities.Subscriptions;

/// <summary>
/// Hóa đơn nội bộ / biên nhận thanh toán gói dịch vụ (F19). <see cref="Code"/> (vd <c>TS0001234</c>) là nội dung
/// chuyển khoản để webhook SePay đối chiếu. Không phải hóa đơn điện tử theo luật thuế.
/// </summary>
public sealed class Invoice
{
    private Invoice()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = null!;

    public Guid TenantId { get; private set; }

    public Guid PlanId { get; private set; }

    /// <summary>Chụp lại tên gói lúc lập hóa đơn để lịch sử không đổi khi quản trị sửa tên gói.</summary>
    public string PlanName { get; private set; } = null!;

    public int PlanDurationDays { get; private set; }

    public decimal Amount { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>Mã giao dịch SePay đã thanh toán hóa đơn này.</summary>
    public long? SepayTransactionId { get; private set; }

    /// <summary>Mã tham chiếu của ngân hàng (vd <c>MBVCB.3278907687</c>).</summary>
    public string? BankReference { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public static Invoice Create(
        string code,
        Guid tenantId,
        Plan plan,
        Guid createdByUserId,
        DateTimeOffset now,
        TimeSpan lifetime) => new()
    {
        Id = Guid.CreateVersion7(now),
        Code = code,
        TenantId = tenantId,
        PlanId = plan.Id,
        PlanName = plan.Name,
        PlanDurationDays = plan.DurationDays,
        Amount = plan.Price,
        Status = InvoiceStatus.Pending,
        CreatedAt = now,
        ExpiresAt = now + lifetime,
        CreatedByUserId = createdByUserId
    };

    /// <summary>
    /// Ghi nhận thanh toán. Hóa đơn đã quá hạn chờ nhưng khách vẫn chuyển đúng tiền thì vẫn nhận (không để khách mất tiền).
    /// Trả false nếu hóa đơn đã thanh toán trước đó.
    /// </summary>
    public bool MarkPaid(long sepayTransactionId, string? bankReference, DateTimeOffset now)
    {
        if (Status == InvoiceStatus.Paid)
        {
            return false;
        }

        Status = InvoiceStatus.Paid;
        PaidAt = now;
        SepayTransactionId = sepayTransactionId;
        BankReference = bankReference;
        return true;
    }

    public bool MarkExpiredIfDue(DateTimeOffset now)
    {
        if (Status != InvoiceStatus.Pending || now < ExpiresAt)
        {
            return false;
        }

        Status = InvoiceStatus.Expired;
        return true;
    }
}
