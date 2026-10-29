using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using TrustSign.Common;
using TrustSign.Domain.Entities.Subscriptions;
using TrustSign.Domain.IRepositories.Subscriptions;

namespace TrustSign.Application.Modules.Subscriptions;

internal sealed class SubscriptionService(
    ISubscriptionRepository subscriptions,
    IInvoiceRepository invoices,
    IPlanRepository plans,
    IOptions<SepayOptions> sepayOptions,
    TimeProvider timeProvider) : ISubscriptionService
{
    private static readonly TimeSpan InvoiceLifetime = TimeSpan.FromHours(24);
    private readonly SepayOptions _sepayOptions = sepayOptions.Value;

    public async Task<Result<CurrentSubscriptionDto>> GetCurrentSubscriptionAsync(
        Guid tenantId,
        CancellationToken cancel = default)
    {
        Subscription? sub = await subscriptions.FindByTenantIdAsync(tenantId, cancel);
        if (sub is null)
        {
            return SubscriptionErrors.SubscriptionNotFound;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Plan? plan = await plans.FindByIdAsync(sub.PlanId, cancel);

        return new CurrentSubscriptionDto(
            sub.PlanId,
            plan?.Name ?? "Gói dịch vụ",
            plan?.Price ?? 0,
            plan?.DurationDays ?? 30,
            sub.EffectiveStatus(now).ToString(),
            sub.ExpiresAt,
            sub.QuotaUsed,
            sub.QuotaLimit);
    }

    public async Task<Result<IReadOnlyList<InvoiceDto>>> GetInvoicesAsync(
        Guid tenantId,
        CancellationToken cancel = default)
    {
        IReadOnlyList<Invoice> list = await invoices.ListByTenantAsync(tenantId, cancel);
        return list.Select(ToDto).ToList();
    }

    public async Task<Result<CheckoutDto>> CreateInvoiceAsync(
        Guid tenantId,
        Guid userId,
        CreateInvoiceRequestDto request,
        CancellationToken cancel = default)
    {
        Plan? plan = await plans.FindByIdAsync(request.PlanId, cancel);
        if (plan is null)
        {
            return SubscriptionErrors.PlanNotFound;
        }

        if (!plan.IsActive || plan.IsTrial)
        {
            return SubscriptionErrors.PlanNotPurchasable;
        }

        if (plan.Price <= 0)
        {
            return Error.Validation("Subscription.FreePlanCannotInvoice", "Gói dùng thử hoặc miễn phí không thể tạo hóa đơn thanh toán.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        string code = await GenerateUniqueInvoiceCodeAsync(cancel);

        Invoice invoice = Invoice.Create(
            code,
            tenantId,
            plan,
            userId,
            now,
            InvoiceLifetime);

        await invoices.AddAsync(invoice, cancel);

        string qrUrl = _sepayOptions.QrTemplateUrl
            .Replace("{acc}", Uri.EscapeDataString(_sepayOptions.BankAccount))
            .Replace("{bank}", Uri.EscapeDataString(_sepayOptions.BankCode))
            .Replace("{amount}", ((long)invoice.Amount).ToString())
            .Replace("{des}", Uri.EscapeDataString(invoice.Code));

        return new CheckoutDto(
            invoice.Code,
            qrUrl,
            _sepayOptions.BankAccount,
            _sepayOptions.BankName,
            invoice.Amount,
            invoice.ExpiresAt);
    }

    public async Task<Result<InvoiceStatusDto>> GetInvoiceStatusAsync(
        Guid tenantId,
        string code,
        CancellationToken cancel = default)
    {
        Invoice? invoice = await invoices.FindByCodeAsync(code.Trim().ToUpperInvariant(), cancel);
        if (invoice is null || invoice.TenantId != tenantId)
        {
            return SubscriptionErrors.InvoiceNotFound;
        }

        return new InvoiceStatusDto(invoice.Status.ToString());
    }

    private async Task<string> GenerateUniqueInvoiceCodeAsync(CancellationToken cancel)
    {
        for (int i = 0; i < 10; i++)
        {
            int num = RandomNumberGenerator.GetInt32(1000000, 9999999);
            string code = $"TS{num}";
            if (!await invoices.CodeExistsAsync(code, cancel))
            {
                return code;
            }
        }

        // Fallback with timestamp suffix
        return $"TS{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 10000000:D7}";
    }

    private static InvoiceDto ToDto(Invoice inv) => new(
        inv.Code,
        inv.PlanName,
        inv.Amount,
        inv.Status.ToString(),
        inv.CreatedAt,
        inv.PaidAt);
}
