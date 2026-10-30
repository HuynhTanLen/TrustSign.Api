using TrustSign.Common;
using TrustSign.Domain.Entities.Contracts;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Contracts;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Contracts;

internal sealed class TemplateService(
    IContractTemplateRepository templates,
    IUserRepository users,
    TimeProvider timeProvider) : ITemplateService
{
    public async Task<Result<IReadOnlyList<TemplateSummaryDto>>> ListAsync(
        Guid tenantId,
        CancellationToken cancel = default)
    {
        IReadOnlyList<ContractTemplate> items = await templates.ListByTenantAsync(tenantId, cancel);
        return items.Select(ToSummaryDto).ToList();
    }

    public async Task<Result<TemplateDetailDto>> GetByIdAsync(
        Guid tenantId,
        Guid templateId,
        CancellationToken cancel = default)
    {
        ContractTemplate? template = await templates.FindByIdAsync(tenantId, templateId, cancel);
        if (template is null)
        {
            return ContractErrors.TemplateNotFound;
        }

        return ToDetailDto(template);
    }

    public async Task<Result<TemplateSummaryDto>> CreateAsync(
        Guid tenantId,
        Guid userId,
        SaveTemplateRequestDto request,
        CancellationToken cancel = default)
    {
        User? creator = await users.FindByIdAsync(userId, cancel);
        string authorName = creator?.FullName ?? "Thành viên";

        DateTimeOffset now = timeProvider.GetUtcNow();
        Result<ContractTemplate> createResult = ContractTemplate.Create(
            tenantId,
            request.Name,
            request.Content,
            userId,
            authorName,
            now);

        if (createResult.IsFailure)
        {
            return createResult.Error;
        }

        ContractTemplate template = createResult.Value;
        await templates.AddAsync(template, cancel);
        return ToSummaryDto(template);
    }

    public async Task<Result<TemplateSummaryDto>> UpdateAsync(
        Guid tenantId,
        Guid templateId,
        SaveTemplateRequestDto request,
        CancellationToken cancel = default)
    {
        ContractTemplate? template = await templates.FindByIdAsync(tenantId, templateId, cancel);
        if (template is null)
        {
            return ContractErrors.TemplateNotFound;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Result updateResult = template.Update(request.Name, request.Content, now);
        if (updateResult.IsFailure)
        {
            return updateResult.Error;
        }

        await templates.SaveChangesAsync(cancel);
        return ToSummaryDto(template);
    }

    public async Task<Result> DeleteAsync(
        Guid tenantId,
        Guid templateId,
        CancellationToken cancel = default)
    {
        ContractTemplate? template = await templates.FindByIdAsync(tenantId, templateId, cancel);
        if (template is null)
        {
            return ContractErrors.TemplateNotFound;
        }

        templates.Remove(template);
        await templates.SaveChangesAsync(cancel);
        return Result.Success();
    }

    private static TemplateSummaryDto ToSummaryDto(ContractTemplate t) => new(
        t.Id,
        t.Name,
        t.GetVariables(),
        t.AuthorName,
        t.UpdatedAt);

    private static TemplateDetailDto ToDetailDto(ContractTemplate t) => new(
        t.Id,
        t.Name,
        t.Content,
        t.GetVariables(),
        t.AuthorName,
        t.UpdatedAt);
}
