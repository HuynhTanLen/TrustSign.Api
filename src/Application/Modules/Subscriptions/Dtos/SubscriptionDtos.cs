namespace TrustSign.Application.Modules.Subscriptions;

public sealed record CurrentSubscriptionDto(
    Guid PlanId,
    string PlanName,
    decimal Price,
    int DurationDays,
    string Status,
    DateTimeOffset ExpiresAt,
    int QuotaUsed,
    int QuotaLimit);
