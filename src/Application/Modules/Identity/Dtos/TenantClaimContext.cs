using TrustSign.Common.Enums;

namespace TrustSign.Application.Modules.Identity;

public sealed record TenantClaimContext(Guid TenantId, TenantRole Role, TenantStatus Status);
