using TrustSign.Common;
using TrustSign.Common.Enums;

namespace TrustSign.Domain.Entities.Identity;

public sealed class TenantInvitation
{
    private TenantInvitation()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Email { get; private set; } = null!;

    public TenantRole Role { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public InvitationStatus Status { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    public static TenantInvitation Create(
        Guid tenantId,
        string email,
        TenantRole role,
        string tokenHash,
        Guid invitedByUserId,
        DateTimeOffset now,
        TimeSpan lifetime) => new()
    {
        Id = Guid.CreateVersion7(now),
        TenantId = tenantId,
        Email = User.NormalizeEmail(email),
        Role = role,
        TokenHash = tokenHash,
        Status = InvitationStatus.Pending,
        InvitedByUserId = invitedByUserId,
        ExpiresAt = now + lifetime,
        CreatedAt = now
    };

    public bool IsPendingValid(DateTimeOffset now) =>
        Status == InvitationStatus.Pending && now < ExpiresAt;

    public Result Accept(string userEmail, DateTimeOffset now)
    {
        if (!IsPendingValid(now))
        {
            return IdentityErrors.InvitationNotFound;
        }

        if (!string.Equals(Email, User.NormalizeEmail(userEmail), StringComparison.Ordinal))
        {
            return IdentityErrors.InvitationEmailMismatch;
        }

        Status = InvitationStatus.Accepted;
        RespondedAt = now;
        return Result.Success();
    }

    public Result Revoke(DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending)
        {
            return IdentityErrors.InvitationNotFound;
        }

        Status = InvitationStatus.Revoked;
        RespondedAt = now;
        return Result.Success();
    }
}
