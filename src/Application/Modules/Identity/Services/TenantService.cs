using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using TrustSign.Application.Modules.Notifications;
using TrustSign.Application.Modules.Subscriptions;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Identity;

internal sealed class TenantService(
    IUserRepository users,
    ITenantRepository tenants,
    IRefreshTokenRepository refreshTokens,
    IAccessTokenIssuer tokenIssuer,
    IEmailSender emailSender,
    ISubscriptionProvisioner subscriptionProvisioner,
    IOptions<SmtpOptions> smtpOptions,
    TimeProvider timeProvider) : ITenantService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

    public async Task<Result<TenantSessionResponseDto>> CreateTenantAsync(
        Guid userId,
        Guid sessionId,
        CreateTenantRequestDto request,
        CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
        {
            return IdentityErrors.InvalidTenantName;
        }

        if (!string.IsNullOrWhiteSpace(request.ContactEmail) && !IsValidEmail(request.ContactEmail))
        {
            return IdentityErrors.InvalidEmail;
        }

        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return IdentityErrors.SessionInvalid;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Tenant tenant = Tenant.Create(
            request.Name,
            request.TaxCode,
            request.Address,
            request.ContactEmail,
            user.Id,
            now);

        Membership ownerMembership = Membership.Create(tenant.Id, user.Id, TenantRole.TenantOwner, now);
        await tenants.AddTenantWithOwnerAsync(tenant, ownerMembership, cancel);

        await subscriptionProvisioner.ProvisionTrialSubscriptionAsync(tenant.Id, now, cancel);

        await UpdateSessionActiveTenantAsync(sessionId, tenant.Id, now, cancel);

        IReadOnlyList<UserTenantMembership> memberships = await tenants.GetUserTenantsAsync(user.Id, cancel);
        AccessTokenDto access = tokenIssuer.Issue(
            user.Id,
            sessionId,
            user.Role,
            new TenantClaimContext(tenant.Id, TenantRole.TenantOwner, tenant.Status));

        return new TenantSessionResponseDto(
            ToTenantDto(tenant, TenantRole.TenantOwner),
            access.AccessToken,
            access.ExpiresAt,
            user.ToProfileDto(memberships, tenant.Id));
    }

    public async Task<Result<IReadOnlyList<TenantDto>>> GetMyTenantsAsync(Guid userId, CancellationToken cancel)
    {
        IReadOnlyList<UserTenantMembership> memberships = await tenants.GetUserTenantsAsync(userId, cancel);
        return memberships
            .Select(item => ToTenantDto(item.Tenant, item.Membership.Role))
            .ToList();
    }

    public async Task<Result<AuthResponseDto>> SwitchTenantAsync(
        Guid userId,
        Guid sessionId,
        Guid targetTenantId,
        CancellationToken cancel)
    {
        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return IdentityErrors.SessionInvalid;
        }

        IReadOnlyList<UserTenantMembership> memberships = await tenants.GetUserTenantsAsync(userId, cancel);
        UserTenantMembership? target = memberships.FirstOrDefault(item => item.Tenant.Id == targetTenantId);
        if (target is null)
        {
            return IdentityErrors.TenantNotFound;
        }

        if (target.Tenant.Status == TenantStatus.Locked)
        {
            return IdentityErrors.TenantLocked;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        await UpdateSessionActiveTenantAsync(sessionId, target.Tenant.Id, now, cancel);

        AccessTokenDto access = tokenIssuer.Issue(
            user.Id,
            sessionId,
            user.Role,
            new TenantClaimContext(target.Tenant.Id, target.Membership.Role, target.Tenant.Status));

        return new AuthResponseDto(
            access.AccessToken,
            access.ExpiresAt,
            user.ToProfileDto(memberships, target.Tenant.Id));
    }

    public async Task<Result<IReadOnlyList<TenantMemberDto>>> GetMembersAsync(Guid tenantId, CancellationToken cancel)
    {
        IReadOnlyList<Membership> memberships = await tenants.GetMembersAsync(tenantId, cancel);
        Guid[] userIds = memberships.Select(item => item.UserId).Distinct().ToArray();
        IReadOnlyDictionary<Guid, User> usersById = await users.FindByIdsAsync(userIds, cancel);

        List<TenantMemberDto> result = [];
        foreach (Membership membership in memberships)
        {
            if (usersById.TryGetValue(membership.UserId, out User? memberUser))
            {
                result.Add(new TenantMemberDto(
                    memberUser.Id,
                    memberUser.FullName,
                    memberUser.Email,
                    membership.Role.ToString(),
                    membership.JoinedAt));
            }
        }

        return result;
    }

    public async Task<Result<TenantMemberDto>> ChangeMemberRoleAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid targetUserId,
        string newRole,
        CancellationToken cancel)
    {
        _ = actorUserId;
        if (!Enum.TryParse(newRole, ignoreCase: true, out TenantRole parsedRole))
        {
            return Error.Validation("Tenant.InvalidRole", "Vai trò thành viên không hợp lệ.");
        }

        IReadOnlyList<Membership> allMembers = await tenants.GetMembersAsync(tenantId, cancel);
        Membership? target = allMembers.FirstOrDefault(item => item.UserId == targetUserId);
        if (target is null)
        {
            return IdentityErrors.MemberNotFound;
        }

        if (target.Role == TenantRole.TenantOwner && parsedRole != TenantRole.TenantOwner)
        {
            int ownerCount = allMembers.Count(item => item.Role == TenantRole.TenantOwner);
            if (ownerCount <= 1)
            {
                return IdentityErrors.CannotRemoveLastOwner;
            }
        }

        target.ChangeRole(parsedRole);
        await tenants.SaveChangesAsync(cancel);

        User? memberUser = await users.FindByIdAsync(targetUserId, cancel);
        if (memberUser is null)
        {
            return IdentityErrors.MemberNotFound;
        }

        return new TenantMemberDto(
            memberUser.Id,
            memberUser.FullName,
            memberUser.Email,
            target.Role.ToString(),
            target.JoinedAt);
    }

    public async Task<Result> RemoveMemberAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancel)
    {
        if (actorUserId == targetUserId)
        {
            return IdentityErrors.CannotRemoveSelf;
        }

        IReadOnlyList<Membership> allMembers = await tenants.GetMembersAsync(tenantId, cancel);
        Membership? target = allMembers.FirstOrDefault(item => item.UserId == targetUserId);
        if (target is null)
        {
            return IdentityErrors.MemberNotFound;
        }

        if (target.Role == TenantRole.TenantOwner)
        {
            int ownerCount = allMembers.Count(item => item.Role == TenantRole.TenantOwner);
            if (ownerCount <= 1)
            {
                return IdentityErrors.CannotRemoveLastOwner;
            }
        }

        tenants.RemoveMembership(target);
        await tenants.SaveChangesAsync(cancel);
        return Result.Success();
    }

    public async Task<Result<TenantInvitationDto>> InviteMemberAsync(
        Guid tenantId,
        Guid inviterUserId,
        CreateInvitationRequestDto request,
        CancellationToken cancel)
    {
        if (!IsValidEmail(request.Email))
        {
            return IdentityErrors.InvalidEmail;
        }

        if (!Enum.TryParse(request.Role, ignoreCase: true, out TenantRole parsedRole))
        {
            return Error.Validation("Tenant.InvalidRole", "Vai trò thành viên không hợp lệ.");
        }

        Tenant? tenant = await tenants.FindByIdAsync(tenantId, cancel);
        if (tenant is null)
        {
            return IdentityErrors.TenantNotFound;
        }

        User? existingUser = await users.FindByEmailAsync(request.Email, cancel);
        if (existingUser is not null)
        {
            Membership? existingMembership = await tenants.FindMembershipAsync(tenantId, existingUser.Id, cancel);
            if (existingMembership is not null)
            {
                return IdentityErrors.AlreadyMember;
            }
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        (string rawToken, string tokenHash) = TokenGenerator.GeneratePair();

        TenantInvitation invitation = TenantInvitation.Create(
            tenantId,
            request.Email,
            parsedRole,
            tokenHash,
            inviterUserId,
            now,
            InvitationLifetime);

        await tenants.AddInvitationAsync(invitation, cancel);

        string baseUrl = _smtpOptions.FrontendBaseUrl.TrimEnd('/');
        string acceptUrl = $"{baseUrl}/invitations/{Uri.EscapeDataString(rawToken)}";
        string safeTenantName = WebUtility.HtmlEncode(tenant.Name);
        string roleDisplay = parsedRole switch
        {
            TenantRole.TenantOwner => "Chủ doanh nghiệp",
            TenantRole.ContractManager => "Quản lý hợp đồng",
            _ => "Nhân viên"
        };

        string html = $"""
            <div style="font-family:'Segoe UI',Roboto,Arial,sans-serif;max-width:560px;margin:0 auto;color:#1f2328;line-height:1.6;">
              <h2 style="color:#1f5fa8;margin-bottom:12px;">Lời mời tham gia doanh nghiệp</h2>
              <p>Bạn đã được mời tham gia không gian làm việc của <strong>{safeTenantName}</strong> trên hệ thống Chữ Tín với vai trò <strong>{roleDisplay}</strong>.</p>
              <p style="margin:24px 0;">
                <a href="{acceptUrl}" style="background:#1f5fa8;color:#ffffff;text-decoration:none;padding:10px 20px;border-radius:4px;display:inline-block;font-weight:600;">
                  Xem và chấp nhận lời mời
                </a>
              </p>
              <p style="font-size:13px;color:#5f6b7a;">Đường dẫn trực tiếp: <a href="{acceptUrl}">{acceptUrl}</a></p>
              <p style="font-size:12px;color:#5f6b7a;">Lời mời có hiệu lực trong 7 ngày.</p>
            </div>
            """;

        await emailSender.SendAsync(
            new EmailMessage(invitation.Email, $"[Chữ Tín] Lời mời tham gia {tenant.Name}", html),
            cancel);

        return new TenantInvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.Role.ToString(),
            invitation.CreatedAt,
            invitation.ExpiresAt);
    }

    public async Task<Result<IReadOnlyList<TenantInvitationDto>>> GetPendingInvitationsAsync(
        Guid tenantId,
        CancellationToken cancel)
    {
        IReadOnlyList<TenantInvitation> list = await tenants.GetPendingInvitationsAsync(
            tenantId,
            timeProvider.GetUtcNow(),
            cancel);

        return list
            .Select(item => new TenantInvitationDto(
                item.Id,
                item.Email,
                item.Role.ToString(),
                item.CreatedAt,
                item.ExpiresAt))
            .ToList();
    }

    public async Task<Result> RevokeInvitationAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken cancel)
    {
        TenantInvitation? invitation = await tenants.FindInvitationByIdAsync(invitationId, cancel);
        if (invitation is null || invitation.TenantId != tenantId)
        {
            return IdentityErrors.InvitationNotFound;
        }

        Result revoke = invitation.Revoke(timeProvider.GetUtcNow());
        if (revoke.IsFailure)
        {
            return revoke.Error;
        }

        await tenants.SaveChangesAsync(cancel);
        return Result.Success();
    }

    public async Task<Result<TenantSessionResponseDto>> AcceptInvitationAsync(
        Guid userId,
        Guid sessionId,
        string rawToken,
        CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return IdentityErrors.InvitationNotFound;
        }

        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return IdentityErrors.SessionInvalid;
        }

        string tokenHash = TokenGenerator.HashToken(rawToken.Trim());
        TenantInvitation? invitation = await tenants.FindInvitationByTokenHashAsync(tokenHash, cancel);
        if (invitation is null)
        {
            return IdentityErrors.InvitationNotFound;
        }

        Tenant? tenant = await tenants.FindByIdAsync(invitation.TenantId, cancel);
        if (tenant is null)
        {
            return IdentityErrors.TenantNotFound;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Result accept = invitation.Accept(user.Email, now);
        if (accept.IsFailure)
        {
            return accept.Error;
        }

        Membership? existingMembership = await tenants.FindMembershipAsync(tenant.Id, user.Id, cancel);
        TenantRole effectiveRole = existingMembership?.Role ?? invitation.Role;

        if (existingMembership is null)
        {
            Membership membership = Membership.Create(tenant.Id, user.Id, invitation.Role, now);
            await tenants.AddMembershipAsync(membership, cancel);
        }
        else
        {
            await tenants.SaveChangesAsync(cancel);
        }

        await UpdateSessionActiveTenantAsync(sessionId, tenant.Id, now, cancel);

        IReadOnlyList<UserTenantMembership> memberships = await tenants.GetUserTenantsAsync(user.Id, cancel);
        AccessTokenDto access = tokenIssuer.Issue(
            user.Id,
            sessionId,
            user.Role,
            new TenantClaimContext(tenant.Id, effectiveRole, tenant.Status));

        return new TenantSessionResponseDto(
            ToTenantDto(tenant, effectiveRole),
            access.AccessToken,
            access.ExpiresAt,
            user.ToProfileDto(memberships, tenant.Id));
    }

    private async Task UpdateSessionActiveTenantAsync(
        Guid sessionId,
        Guid tenantId,
        DateTimeOffset now,
        CancellationToken cancel)
    {
        RefreshToken? activeRefresh = await refreshTokens.FindActiveBySessionIdAsync(sessionId, now, cancel);
        if (activeRefresh is not null)
        {
            activeRefresh.SetActiveTenant(tenantId);
            await refreshTokens.SaveChangesAsync(cancel);
        }
    }

    private static TenantDto ToTenantDto(Tenant tenant, TenantRole role) => new(
        tenant.Id,
        tenant.Name,
        tenant.TaxCode,
        tenant.Address,
        tenant.ContactEmail,
        tenant.Status.ToString(),
        role.ToString(),
        tenant.CreatedAt);

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        string trimmed = email.Trim();
        return trimmed.Length <= 254 && MailAddress.TryCreate(trimmed, out MailAddress? parsed) && parsed.Address == trimmed;
    }
}
