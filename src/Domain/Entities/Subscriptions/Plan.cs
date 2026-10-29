using TrustSign.Common;

namespace TrustSign.Domain.Entities.Subscriptions;

/// <summary>Gói dịch vụ SaaS (F17): giá, số ngày mỗi kỳ, hạn mức hợp đồng gửi ký mỗi tháng, cờ dùng thử.</summary>
public sealed class Plan
{
    private Plan()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>Giá mỗi kỳ, đơn vị VND.</summary>
    public decimal Price { get; private set; }

    public int DurationDays { get; private set; }

    public int MonthlyQuota { get; private set; }

    public bool IsTrial { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Plan> Create(
        string name,
        decimal price,
        int durationDays,
        int monthlyQuota,
        bool isTrial,
        bool isActive,
        DateTimeOffset now)
    {
        Result validation = Validate(name, price, durationDays, monthlyQuota);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        return new Plan
        {
            Id = Guid.CreateVersion7(now),
            Name = name.Trim(),
            Price = price,
            DurationDays = durationDays,
            MonthlyQuota = monthlyQuota,
            IsTrial = isTrial,
            IsActive = isActive,
            CreatedAt = now
        };
    }

    public Result Update(string name, decimal price, int durationDays, int monthlyQuota, bool isTrial, bool isActive)
    {
        Result validation = Validate(name, price, durationDays, monthlyQuota);
        if (validation.IsFailure)
        {
            return validation;
        }

        Name = name.Trim();
        Price = price;
        DurationDays = durationDays;
        MonthlyQuota = monthlyQuota;
        IsTrial = isTrial;
        IsActive = isActive;
        return Result.Success();
    }

    private static Result Validate(string name, decimal price, int durationDays, int monthlyQuota)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > 100 || price < 0 || durationDays <= 0 || monthlyQuota <= 0)
        {
            return SubscriptionErrors.InvalidPlan;
        }

        return Result.Success();
    }
}
