using TrustSign.Common;
using TrustSign.Common.Enums;

namespace TrustSign.Api.Security;

public static class AuthorizationPolicies
{
    public const string TenantMember = "TenantMember";
    public const string ContractManagerOrOwner = "ContractManagerOrOwner";
    public const string TenantOwnerOnly = "TenantOwnerOnly";

    public static IServiceCollection AddTrustSignAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(TenantMember, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(JwtClaims.TenantId)
                .RequireClaim(JwtClaims.TenantStatus, nameof(TenantStatus.Active)))
            .AddPolicy(ContractManagerOrOwner, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(JwtClaims.TenantId)
                .RequireClaim(JwtClaims.TenantStatus, nameof(TenantStatus.Active))
                .RequireClaim(JwtClaims.TenantRole, nameof(TenantRole.TenantOwner), nameof(TenantRole.ContractManager)))
            .AddPolicy(TenantOwnerOnly, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(JwtClaims.TenantId)
                .RequireClaim(JwtClaims.TenantStatus, nameof(TenantStatus.Active))
                .RequireClaim(JwtClaims.TenantRole, nameof(TenantRole.TenantOwner)));

        return services;
    }
}
