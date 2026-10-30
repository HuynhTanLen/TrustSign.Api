using TrustSign.Common;

namespace TrustSign.Application.Modules.Contracts;

public interface ITemplateService
{
    Task<Result<IReadOnlyList<TemplateSummaryDto>>> ListAsync(Guid tenantId, CancellationToken cancel = default);

    Task<Result<TemplateDetailDto>> GetByIdAsync(Guid tenantId, Guid templateId, CancellationToken cancel = default);

    Task<Result<TemplateSummaryDto>> CreateAsync(
        Guid tenantId,
        Guid userId,
        SaveTemplateRequestDto request,
        CancellationToken cancel = default);

    Task<Result<TemplateSummaryDto>> UpdateAsync(
        Guid tenantId,
        Guid templateId,
        SaveTemplateRequestDto request,
        CancellationToken cancel = default);

    Task<Result> DeleteAsync(Guid tenantId, Guid templateId, CancellationToken cancel = default);
}
