using TrustSign.Domain.Entities.Subscriptions;

namespace TrustSign.Domain.IRepositories.Subscriptions;

public interface IInvoiceRepository
{
    Task<Invoice?> FindByCodeAsync(string code, CancellationToken cancel);

    Task<IReadOnlyList<Invoice>> ListByTenantAsync(Guid tenantId, CancellationToken cancel);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancel);

    Task AddAsync(Invoice invoice, CancellationToken cancel);

    /// <summary>Chuyển hóa đơn chờ thanh toán đã quá hạn sang Hết hạn, trả số bản ghi đã đổi.</summary>
    Task<int> ExpireDueAsync(DateTimeOffset now, CancellationToken cancel);

    Task<bool> TransactionExistsAsync(long sepayTransactionId, CancellationToken cancel);

    Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancel);

    Task SaveChangesAsync(CancellationToken cancel);
}
