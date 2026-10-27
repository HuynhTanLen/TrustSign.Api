namespace TrustSign.Application.Modules.Identity;

public sealed record CreateTenantRequestDto(
    string Name,
    string? TaxCode,
    string? Address,
    string? ContactEmail);

public sealed record TenantDto(
    Guid Id,
    string Name,
    string? TaxCode,
    string? Address,
    string? ContactEmail,
    string Status,
    string Role,
    DateTimeOffset CreatedAt);

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
