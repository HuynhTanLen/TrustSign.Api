using TrustSign.Domain.Entities.Contracts;

namespace TrustSign.Domain.IRepositories.Contracts;

public interface IContractTemplateRepository
{
    Task<IReadOnlyList<ContractTemplate>> ListByTenantAsync(Guid tenantId, CancellationToken cancel);

    /// <summary>Tìm mẫu hợp đồng thuộc đúng doanh nghiệp <paramref name="tenantId"/> (cách ly đa khách thuê TC10, TC14).</summary>
    Task<ContractTemplate?> FindByIdAsync(Guid tenantId, Guid templateId, CancellationToken cancel);

    Task AddAsync(ContractTemplate template, CancellationToken cancel);

    void Remove(ContractTemplate template);

    Task SaveChangesAsync(CancellationToken cancel);
}
