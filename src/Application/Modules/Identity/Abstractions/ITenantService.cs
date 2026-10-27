using TrustSign.Common;

namespace TrustSign.Application.Modules.Identity;

public interface ITenantService
{
    Task<Result<TenantSessionResponseDto>> CreateTenantAsync(
        Guid userId,
        Guid sessionId,
        CreateTenantRequestDto request,
        CancellationToken cancel);

    Task<Result<IReadOnlyList<TenantDto>>> GetMyTenantsAsync(Guid userId, CancellationToken cancel);

    Task<Result<AuthResponseDto>> SwitchTenantAsync(
        Guid userId,
        Guid sessionId,
        Guid targetTenantId,
        CancellationToken cancel);

    Task<Result<IReadOnlyList<TenantMemberDto>>> GetMembersAsync(Guid tenantId, CancellationToken cancel);

    Task<Result<TenantMemberDto>> ChangeMemberRoleAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid targetUserId,
        string newRole,
        CancellationToken cancel);

    Task<Result> RemoveMemberAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancel);

    Task<Result<TenantInvitationDto>> InviteMemberAsync(
        Guid tenantId,
        Guid inviterUserId,
        CreateInvitationRequestDto request,
        CancellationToken cancel);

    Task<Result<IReadOnlyList<TenantInvitationDto>>> GetPendingInvitationsAsync(
        Guid tenantId,
        CancellationToken cancel);

    Task<Result> RevokeInvitationAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken cancel);

    Task<Result<TenantSessionResponseDto>> AcceptInvitationAsync(
        Guid userId,
        Guid sessionId,
        string rawToken,
        CancellationToken cancel);
}
