namespace TrustSign.Application.Modules.Subscriptions;

public sealed record InvoiceDto(
    string Code,
    string PlanName,
    decimal Amount,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt);

public sealed record CheckoutDto(
    string InvoiceCode,
    string? QrImageUrl,
    string BankAccount,
    string BankName,
    decimal Amount,
    DateTimeOffset ExpiresAt);

public sealed record CreateInvoiceRequestDto(Guid PlanId);

public sealed record InvoiceStatusDto(string Status);
