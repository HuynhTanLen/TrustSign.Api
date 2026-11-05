namespace TrustSign.Application.Modules.Identity;

public sealed record CreateTenantRequestDto(
    string Name,
    string? TaxCode = null,
    string? Address = null,
    string? ContactEmail = null,
    string? RepresentativeName = null,
    string? RepresentativeTitle = null,
    string? Phone = null);

public sealed record UpdateTenantRequestDto(
    string Name,
    string? TaxCode = null,
    string? Address = null,
    string? ContactEmail = null,
    string? Phone = null,
    string? RepresentativeName = null,
    string? RepresentativeTitle = null);

public sealed record TenantDto(
    Guid Id,
    string Name,
    string? TaxCode,
    string? Address,
    string? ContactEmail,
    string Status,
    string Role,
    DateTimeOffset CreatedAt,
    string? RepresentativeName = null,
    string? RepresentativeTitle = null,
    string? Phone = null);

public sealed record TenantSessionResponseDto(
    TenantDto Tenant,
    string AccessToken,
    DateTimeOffset ExpiresAt,
    UserProfileDto User);

public sealed record TenantMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    DateTimeOffset JoinedAt);

public sealed record ChangeMemberRoleRequestDto(string Role);

public sealed record CreateInvitationRequestDto(string Email, string Role);

public sealed record TenantInvitationDto(
    Guid Id,
    string Email,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
