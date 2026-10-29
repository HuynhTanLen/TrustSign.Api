using TrustSign.Common.Enums;

namespace TrustSign.Domain.Entities.Subscriptions;

/// <summary>
/// Thuê bao của một doanh nghiệp (F18, F20). Mỗi doanh nghiệp có đúng một bản ghi.
/// Hạn mức tính theo kỳ một tháng bắt đầu từ <see cref="QuotaPeriodStart"/>; sang kỳ mới thì <see cref="QuotaUsed"/> về 0.
/// </summary>
public sealed class Subscription
{
    private Subscription()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Mã doanh nghiệp bên module Identity (không đặt khóa ngoại sang schema khác).</summary>
    public Guid TenantId { get; private set; }

    public Guid PlanId { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public int QuotaLimit { get; private set; }

    public int QuotaUsed { get; private set; }

    public DateTimeOffset QuotaPeriodStart { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Subscription StartTrial(Guid tenantId, Plan trialPlan, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        TenantId = tenantId,
        PlanId = trialPlan.Id,
        Status = SubscriptionStatus.Trial,
        StartedAt = now,
        ExpiresAt = now.AddDays(trialPlan.DurationDays),
        QuotaLimit = trialPlan.MonthlyQuota,
        QuotaUsed = 0,
        QuotaPeriodStart = now,
        UpdatedAt = now
    };

    /// <summary>Trạng thái thực tế tại thời điểm <paramref name="now"/>, kể cả khi tác vụ nền chưa kịp quét.</summary>
    public SubscriptionStatus EffectiveStatus(DateTimeOffset now) =>
        now >= ExpiresAt ? SubscriptionStatus.Expired : Status;

    /// <summary>
    /// Áp dụng một hóa đơn đã thanh toán (F19):
    /// gia hạn cùng gói đang hiệu lực thì cộng dồn thêm số ngày; đổi gói, đang dùng thử hoặc đã hết hạn thì tính lại từ bây giờ.
    /// </summary>
    public void ApplyPaidPlan(Plan plan, DateTimeOffset now)
    {
        bool renewSameActivePlan = PlanId == plan.Id
            && Status == SubscriptionStatus.Active
            && ExpiresAt > now;

        if (renewSameActivePlan)
        {
            ExpiresAt = ExpiresAt.AddDays(plan.DurationDays);
        }
        else
        {
            PlanId = plan.Id;
            StartedAt = now;
            ExpiresAt = now.AddDays(plan.DurationDays);
        }

        if (Status == SubscriptionStatus.Expired)
        {
            QuotaUsed = 0;
            QuotaPeriodStart = now;
        }

        Status = SubscriptionStatus.Active;
        QuotaLimit = plan.MonthlyQuota;
        UpdatedAt = now;
    }

    /// <summary>Chuyển sang Hết hạn nếu đã quá hạn. Trả true nếu có thay đổi.</summary>
    public bool MarkExpiredIfDue(DateTimeOffset now)
    {
        if (Status == SubscriptionStatus.Expired || now < ExpiresAt)
        {
            return false;
        }

        Status = SubscriptionStatus.Expired;
        UpdatedAt = now;
        return true;
    }

    /// <summary>Bắt đầu kỳ hạn mức mới nếu đã qua một tháng kể từ đầu kỳ hiện tại.</summary>
    public void RollQuotaPeriodIfDue(DateTimeOffset now)
    {
        if (now >= QuotaPeriodStart.AddMonths(1))
        {
            QuotaUsed = 0;
            QuotaPeriodStart = now;
            UpdatedAt = now;
        }
    }

    /// <summary>
    /// Trừ một lượt hạn mức. Trả true nếu trừ thành công, false nếu đã hết hạn hoặc hết hạn mức.
    /// (Tầng EF Core dùng ExecuteUpdateAsync để nguyên tử ở DB; hàm này dùng cho domain logic và unit test).
    /// </summary>
    public bool TryConsumeQuota(DateTimeOffset now)
    {
        RollQuotaPeriodIfDue(now);
        if (EffectiveStatus(now) == SubscriptionStatus.Expired || QuotaUsed >= QuotaLimit)
        {
            return false;
        }

        QuotaUsed++;
        UpdatedAt = now;
        return true;
    }
}
