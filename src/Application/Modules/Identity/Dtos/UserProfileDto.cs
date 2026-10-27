namespace TrustSign.Application.Modules.Identity;

public sealed record UserTenantDto(Guid Id, string Name, string Role);

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool EmailVerified,
    bool HasTenant,
    Guid? ActiveTenantId,
    IReadOnlyList<UserTenantDto> Tenants);
