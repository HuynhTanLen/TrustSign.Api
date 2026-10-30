namespace TrustSign.Application.Modules.Contracts;

public sealed record TemplateSummaryDto(
    Guid Id,
    string Name,
    IReadOnlyList<string> Variables,
    string AuthorName,
    DateTimeOffset UpdatedAt);

public sealed record TemplateDetailDto(
    Guid Id,
    string Name,
    string Content,
    IReadOnlyList<string> Variables,
    string AuthorName,
    DateTimeOffset UpdatedAt);

public sealed record SaveTemplateRequestDto(
    string Name,
    string Content);
