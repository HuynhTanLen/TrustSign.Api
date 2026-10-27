using TrustSign.Common.Enums;

namespace TrustSign.Domain.Entities.Identity;

public sealed class Membership
{
    private Membership()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public TenantRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public static Membership Create(Guid tenantId, Guid userId, TenantRole role, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        TenantId = tenantId,
        UserId = userId,
        Role = role,
        JoinedAt = now
    };

    public void ChangeRole(TenantRole role)
    {
        Role = role;
    }
}
