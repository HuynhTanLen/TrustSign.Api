using TrustSign.Common;

namespace TrustSign.Application.Modules.Subscriptions;

public interface IQuotaService
{
    /// <summary>
    /// Kiểm tra doanh nghiệp còn hạn mức ký trong chu kỳ hiện tại không.
    /// </summary>
    Task<bool> HasAvailableQuotaAsync(Guid tenantId, CancellationToken cancel = default);

    /// <summary>
    /// Trừ 1 lượt ký theo cơ chế nguyên tử (atomic update tại database, TC15, TC21).
    /// </summary>
    Task<Result> ConsumeQuotaAsync(Guid tenantId, CancellationToken cancel = default);
}
