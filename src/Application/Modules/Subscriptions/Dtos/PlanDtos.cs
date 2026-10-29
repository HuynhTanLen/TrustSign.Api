namespace TrustSign.Application.Modules.Subscriptions;

public sealed record PlanDto(
    Guid Id,
    string Name,
    decimal Price,
    int DurationDays,
    int MonthlyQuota,
    bool IsTrial,
    bool IsActive);

public sealed record SavePlanRequestDto(
    string Name,
    decimal Price,
    int DurationDays,
    int MonthlyQuota,
    bool IsTrial,
    bool IsActive);
