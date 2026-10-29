using TrustSign.Common;

namespace TrustSign.Application.Modules.Subscriptions;

public interface ISubscriptionService
{
    Task<Result<CurrentSubscriptionDto>> GetCurrentSubscriptionAsync(Guid tenantId, CancellationToken cancel = default);

    Task<Result<IReadOnlyList<InvoiceDto>>> GetInvoicesAsync(Guid tenantId, CancellationToken cancel = default);

    Task<Result<CheckoutDto>> CreateInvoiceAsync(Guid tenantId, Guid userId, CreateInvoiceRequestDto request, CancellationToken cancel = default);

    Task<Result<InvoiceStatusDto>> GetInvoiceStatusAsync(Guid tenantId, string code, CancellationToken cancel = default);
}
