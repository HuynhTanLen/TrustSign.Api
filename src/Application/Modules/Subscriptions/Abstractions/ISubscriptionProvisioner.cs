using TrustSign.Common;

namespace TrustSign.Application.Modules.Subscriptions;

public interface ISubscriptionProvisioner
{
    /// <summary>
    /// Tự động khởi tạo gói dùng thử (14 ngày) cho doanh nghiệp mới tạo (SUB-03).
    /// </summary>
    Task<Result> ProvisionTrialSubscriptionAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancel = default);
}
