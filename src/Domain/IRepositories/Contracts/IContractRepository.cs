using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Contracts;

namespace TrustSign.Domain.IRepositories.Contracts;

public interface IContractRepository
{
    /// <summary>
    /// Liệt kê hợp đồng của doanh nghiệp <paramref name="tenantId"/> (kèm danh sách người ký).
    /// Nếu <paramref name="createdByUserId"/> có giá trị (vai trò Nhân viên), chỉ trả hợp đồng do người đó tạo (TC20).
    /// </summary>
    Task<IReadOnlyList<Contract>> ListByTenantAsync(
        Guid tenantId,
        Guid? createdByUserId,
        string? query,
        ContractStatus? status,
        CancellationToken cancel);

    /// <summary>Tìm hợp đồng (kèm người ký) thuộc đúng doanh nghiệp <paramref name="tenantId"/> (TC10, TC14).</summary>
    Task<Contract?> FindByIdAsync(Guid tenantId, Guid contractId, CancellationToken cancel);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancel);

    Task AddAsync(Contract contract, CancellationToken cancel);

    Task ReplaceSignersAsync(Contract contract, IReadOnlyList<ContractSigner> newSigners, CancellationToken cancel);

    Task SaveChangesAsync(CancellationToken cancel);
}
