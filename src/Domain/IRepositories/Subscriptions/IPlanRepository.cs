using TrustSign.Domain.Entities.Subscriptions;

namespace TrustSign.Domain.IRepositories.Subscriptions;

public interface IPlanRepository
{
    Task<IReadOnlyList<Plan>> ListAsync(bool includeInactive, CancellationToken cancel);

    Task<Plan?> FindByIdAsync(Guid planId, CancellationToken cancel);

    Task<IReadOnlyDictionary<Guid, Plan>> FindByIdsAsync(IReadOnlyCollection<Guid> planIds, CancellationToken cancel);

    /// <summary>Gói dùng thử đang mở để cấp cho doanh nghiệp mới.</summary>
    Task<Plan?> FindActiveTrialAsync(CancellationToken cancel);

    Task<bool> AnyAsync(CancellationToken cancel);

    Task AddAsync(Plan plan, CancellationToken cancel);

    Task SaveChangesAsync(CancellationToken cancel);
}
