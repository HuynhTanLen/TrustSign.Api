using Microsoft.EntityFrameworkCore;
using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Contracts;
using TrustSign.Domain.IRepositories.Contracts;

namespace TrustSign.Domain.Repositories.Contracts;

internal sealed class EfContractTemplateRepository(TrustSignDbContext db) : IContractTemplateRepository
{
    public async Task<IReadOnlyList<ContractTemplate>> ListByTenantAsync(Guid tenantId, CancellationToken cancel) =>
        await db.ContractTemplates
            .Where(tpl => tpl.TenantId == tenantId)
            .OrderByDescending(tpl => tpl.UpdatedAt)
            .ToListAsync(cancel);

    public Task<ContractTemplate?> FindByIdAsync(Guid tenantId, Guid templateId, CancellationToken cancel) =>
        db.ContractTemplates
            .FirstOrDefaultAsync(tpl => tpl.Id == templateId && tpl.TenantId == tenantId, cancel);

    public async Task AddAsync(ContractTemplate template, CancellationToken cancel)
    {
        db.ContractTemplates.Add(template);
        await db.SaveChangesAsync(cancel);
    }

    public void Remove(ContractTemplate template) => db.ContractTemplates.Remove(template);

    public Task SaveChangesAsync(CancellationToken cancel) => db.SaveChangesAsync(cancel);
}

internal sealed class EfContractRepository(TrustSignDbContext db) : IContractRepository
{
    public async Task<IReadOnlyList<Contract>> ListByTenantAsync(
        Guid tenantId,
        Guid? createdByUserId,
        string? query,
        ContractStatus? status,
        CancellationToken cancel)
    {
        IQueryable<Contract> q = db.Contracts
            .Include(c => c.Signers)
            .Where(c => c.TenantId == tenantId);

        if (createdByUserId.HasValue)
        {
            q = q.Where(c => c.CreatedByUserId == createdByUserId.Value);
        }

        if (status.HasValue)
        {
            q = q.Where(c => c.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            string pattern = $"%{query.Trim()}%";
            q = q.Where(c =>
                EF.Functions.ILike(c.Title, pattern) ||
                EF.Functions.ILike(c.Code, pattern) ||
                c.Signers.Any(s => EF.Functions.ILike(s.FullName, pattern) || EF.Functions.ILike(s.Email, pattern)));
        }

        return await q
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancel);
    }

    public Task<Contract?> FindByIdAsync(Guid tenantId, Guid contractId, CancellationToken cancel) =>
        db.Contracts
            .Include(c => c.Signers)
            .FirstOrDefaultAsync(c => c.Id == contractId && c.TenantId == tenantId, cancel);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancel) =>
        db.Contracts.AnyAsync(c => c.Code == code, cancel);

    public async Task AddAsync(Contract contract, CancellationToken cancel)
    {
        db.Contracts.Add(contract);
        await db.SaveChangesAsync(cancel);
    }

    public async Task ReplaceSignersAsync(
        Contract contract,
        IReadOnlyList<ContractSigner> newSigners,
        CancellationToken cancel)
    {
        await db.ContractSigners
            .Where(s => s.ContractId == contract.Id)
            .ExecuteDeleteAsync(cancel);

        db.ContractSigners.AddRange(newSigners);
        await db.SaveChangesAsync(cancel);
    }

    public Task SaveChangesAsync(CancellationToken cancel) => db.SaveChangesAsync(cancel);
}
