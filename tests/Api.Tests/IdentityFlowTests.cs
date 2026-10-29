using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using TrustSign.Api.Security;
using TrustSign.Application.Modules.Identity;
using TrustSign.Application.Modules.Notifications;
using TrustSign.Application.Modules.Subscriptions;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Api.Tests;

public sealed class IdentityFlowTests
{
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero));
    private readonly InMemoryIdentityStore _store = new();
    private readonly RecordingEmailSender _emails = new();
    private readonly SigningKeyRing _keyRing = SigningKeyRing.CreateEphemeral();
    private readonly IPasswordHasher _hasher = new Pbkdf2PasswordHasher();
    private readonly RsaAccessTokenIssuer _issuer;
    private readonly IAuthService _auth;
    private readonly ITenantService _tenants;

    public IdentityFlowTests()
    {
        IOptions<JwtOptions> jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "trustsign",
            Audience = "trustsign-clients",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
            VerificationTokenHours = 24,
            PrivateKeyPath = "unused"
        });
        IOptions<SmtpOptions> smtpOptions = Options.Create(new SmtpOptions
        {
            FrontendBaseUrl = "http://localhost:3000"
        });

        _issuer = new RsaAccessTokenIssuer(_keyRing, jwtOptions, _clock);
        _auth = new AuthService(
            _store,
            _store,
            _store,
            _store,
            _hasher,
            _issuer,
            _emails,
            jwtOptions,
            smtpOptions,
            _clock);
        _tenants = new TenantService(
            _store,
            _store,
            _store,
            _issuer,
            _emails,
            new NoOpSubscriptionProvisioner(),
            smtpOptions,
            _clock);
    }

    [Fact]
    public async Task Register_verify_once_and_login_flow_succeeds()
    {
        Result<RegisterResultDto> reg = await _auth.RegisterAsync(
            new RegisterRequestDto("Nguyễn Văn An", "An.Nguyen@TrustSign.vn", "TrustSign@2026"),
            CancellationToken.None);

        Assert.True(reg.IsSuccess);
        Assert.Equal("an.nguyen@trustsign.vn", reg.Value.Email);

        // Chưa xác minh email -> không cho đăng nhập.
        Result<AuthSessionResultDto> unverifiedLogin = await _auth.LoginAsync(
            new LoginRequestDto("an.nguyen@trustsign.vn", "TrustSign@2026"),
            CancellationToken.None);
        Assert.True(unverifiedLogin.IsFailure);
        Assert.Equal(IdentityErrors.EmailNotVerified, unverifiedLogin.Error);

        // Lấy token từ email đã gửi và xác minh.
        string rawVerifyToken = ExtractQueryToken(_emails.LastSent!.HtmlBody);
        Result<ActionMessageDto> verify1 = await _auth.VerifyEmailAsync(rawVerifyToken, CancellationToken.None);
        Assert.True(verify1.IsSuccess);

        // Token xác minh chỉ dùng một lần.
        Result<ActionMessageDto> verify2 = await _auth.VerifyEmailAsync(rawVerifyToken, CancellationToken.None);
        Assert.True(verify2.IsFailure);
        Assert.Equal(IdentityErrors.VerificationTokenInvalid, verify2.Error);

        // Đăng nhập thành công sau khi xác minh.
        Result<AuthSessionResultDto> login = await _auth.LoginAsync(
            new LoginRequestDto("an.nguyen@trustsign.vn", "TrustSign@2026"),
            CancellationToken.None);
        Assert.True(login.IsSuccess);
        Assert.True(login.Value.User.EmailVerified);
        Assert.False(login.Value.User.HasTenant);
    }

    [Fact]
    public async Task Reusing_rotated_refresh_token_is_rejected_and_revokes_entire_session_TC11()
    {
        AuthSessionResultDto initial = await RegisterVerifyAndLoginAsync("owner@trustsign.vn", "Chủ DN");

        // Xoay vòng lần 1 hợp lệ: RT1 -> RT2.
        Result<AuthSessionResultDto> rotated = await _auth.RefreshAsync(initial.RawRefreshToken, CancellationToken.None);
        Assert.True(rotated.IsSuccess);
        Assert.NotEqual(initial.RawRefreshToken, rotated.Value.RawRefreshToken);

        // TC11: Dùng lại RT1 cũ -> bị từ chối với lỗi RefreshTokenReused và thu hồi cả phiên.
        Result<AuthSessionResultDto> replay = await _auth.RefreshAsync(initial.RawRefreshToken, CancellationToken.None);
        Assert.True(replay.IsFailure);
        Assert.Equal(IdentityErrors.RefreshTokenReused, replay.Error);

        // RT2 vừa cấp hợp lệ trước đó cũng không còn dùng được nữa vì phiên đã bị thu hồi!
        Result<AuthSessionResultDto> afterRevoke = await _auth.RefreshAsync(
            rotated.Value.RawRefreshToken,
            CancellationToken.None);
        Assert.True(afterRevoke.IsFailure);
        Assert.Equal(IdentityErrors.RefreshTokenInvalid, afterRevoke.Error);
    }

    [Fact]
    public async Task Tenant_invite_accept_and_role_authorization_policy_TC20()
    {
        AuthSessionResultDto ownerSession = await RegisterVerifyAndLoginAsync("owner@company.vn", "Chủ Doanh Nghiệp");
        Guid ownerSessionId = ParseSessionId(ownerSession.AccessToken);

        // Chủ DN tạo doanh nghiệp -> nhận token mới có claim tenant_id, tenant_role=TenantOwner.
        Result<TenantSessionResponseDto> createdTenant = await _tenants.CreateTenantAsync(
            ownerSession.User.Id,
            ownerSessionId,
            new CreateTenantRequestDto("Công ty Chữ Tín", "0101234567", "Hà Nội", "contact@company.vn"),
            CancellationToken.None);

        Assert.True(createdTenant.IsSuccess);
        Guid tenantId = createdTenant.Value.Tenant.Id;

        // Chủ DN gửi lời mời cho nhân viên.
        Result<TenantInvitationDto> invite = await _tenants.InviteMemberAsync(
            tenantId,
            ownerSession.User.Id,
            new CreateInvitationRequestDto("staff@company.vn", nameof(TenantRole.Employee)),
            CancellationToken.None);
        Assert.True(invite.IsSuccess);
        string inviteToken = ExtractInvitationToken(_emails.LastSent!.HtmlBody);

        // Nhân viên đăng ký, xác minh, đăng nhập và chấp nhận lời mời.
        AuthSessionResultDto staffLogin = await RegisterVerifyAndLoginAsync("staff@company.vn", "Nhân Viên A");
        Result<TenantSessionResponseDto> accepted = await _tenants.AcceptInvitationAsync(
            staffLogin.User.Id,
            ParseSessionId(staffLogin.AccessToken),
            inviteToken,
            CancellationToken.None);

        Assert.True(accepted.IsSuccess);
        Assert.Equal("Employee", accepted.Value.Tenant.Role);

        // Kiểm tra Authorization Policy (IDN-05, TC20): Chủ DN pass TenantOwnerOnly (200), Nhân viên bị chặn (403).
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddTrustSignAuthorization()
            .BuildServiceProvider();
        IAuthorizationService authorization = sp.GetRequiredService<IAuthorizationService>();

        ClaimsPrincipal ownerPrincipal = ToPrincipal(createdTenant.Value.AccessToken);
        ClaimsPrincipal staffPrincipal = ToPrincipal(accepted.Value.AccessToken);

        AuthorizationResult ownerAuth = await authorization.AuthorizeAsync(
            ownerPrincipal,
            resource: null,
            AuthorizationPolicies.TenantOwnerOnly);
        AuthorizationResult staffAuth = await authorization.AuthorizeAsync(
            staffPrincipal,
            resource: null,
            AuthorizationPolicies.TenantOwnerOnly);
        AuthorizationResult staffMemberAuth = await authorization.AuthorizeAsync(
            staffPrincipal,
            resource: null,
            AuthorizationPolicies.TenantMember);

        Assert.True(ownerAuth.Succeeded);
        Assert.False(staffAuth.Succeeded);
        Assert.True(staffMemberAuth.Succeeded);

        // Chặn xóa hoặc hạ quyền Chủ DN cuối cùng.
        Result<TenantMemberDto> demoteLastOwner = await _tenants.ChangeMemberRoleAsync(
            tenantId,
            ownerSession.User.Id,
            ownerSession.User.Id,
            nameof(TenantRole.Employee),
            CancellationToken.None);
        Assert.True(demoteLastOwner.IsFailure);
        Assert.Equal(IdentityErrors.CannotRemoveLastOwner, demoteLastOwner.Error);
    }

    private async Task<AuthSessionResultDto> RegisterVerifyAndLoginAsync(string email, string fullName)
    {
        await _auth.RegisterAsync(new RegisterRequestDto(fullName, email, "TrustSign@2026"), CancellationToken.None);
        string verifyToken = ExtractQueryToken(_emails.LastSent!.HtmlBody);
        await _auth.VerifyEmailAsync(verifyToken, CancellationToken.None);
        Result<AuthSessionResultDto> login = await _auth.LoginAsync(
            new LoginRequestDto(email, "TrustSign@2026"),
            CancellationToken.None);
        Assert.True(login.IsSuccess);
        return login.Value;
    }

    private static string ExtractQueryToken(string html)
    {
        Match match = Regex.Match(html, @"verify-email\?token=([^""&<]+)");
        Assert.True(match.Success, "Không tìm thấy link verify-email trong nội dung email.");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private static string ExtractInvitationToken(string html)
    {
        Match match = Regex.Match(html, @"/invitations/([^""&<]+)");
        Assert.True(match.Success, "Không tìm thấy link lời mời trong nội dung email.");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private static Guid ParseSessionId(string accessToken)
    {
        JsonWebToken jwt = new JsonWebTokenHandler().ReadJsonWebToken(accessToken);
        return Guid.Parse(jwt.GetClaim(JwtClaims.Session).Value);
    }

    private static ClaimsPrincipal ToPrincipal(string accessToken)
    {
        JsonWebToken jwt = new JsonWebTokenHandler().ReadJsonWebToken(accessToken);
        return new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "Bearer", JwtClaims.Subject, JwtClaims.Role));
    }

    private sealed class FakeClock(DateTimeOffset initial) : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = initial;
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public EmailMessage? LastSent { get; private set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancel)
        {
            LastSent = message;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryIdentityStore :
        IUserRepository,
        IEmailVerificationTokenRepository,
        IRefreshTokenRepository,
        ITenantRepository
    {
        private readonly List<User> _users = [];
        private readonly List<EmailVerificationToken> _verificationTokens = [];
        private readonly List<RefreshToken> _refreshTokens = [];
        private readonly List<Tenant> _tenants = [];
        private readonly List<Membership> _memberships = [];
        private readonly List<TenantInvitation> _invitations = [];

        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancel) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

        public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancel) =>
            Task.FromResult<IReadOnlyDictionary<Guid, User>>(
                _users.Where(u => ids.Contains(u.Id)).ToDictionary(u => u.Id));

        public Task<User?> FindByEmailAsync(string email, CancellationToken cancel)
        {
            string normalized = User.NormalizeEmail(email);
            return Task.FromResult(_users.FirstOrDefault(u => u.Email == normalized));
        }

        public Task AddAsync(User user, CancellationToken cancel)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }

        public Task<EmailVerificationToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancel) =>
            Task.FromResult(_verificationTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

        public Task AddAsync(EmailVerificationToken token, CancellationToken cancel)
        {
            _verificationTokens.Add(token);
            return Task.CompletedTask;
        }

        Task<RefreshToken?> IRefreshTokenRepository.FindByTokenHashAsync(string tokenHash, CancellationToken cancel) =>
            Task.FromResult(_refreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

        public Task<RefreshToken?> FindActiveBySessionIdAsync(
            Guid sessionId,
            DateTimeOffset now,
            CancellationToken cancel) =>
            Task.FromResult(_refreshTokens.FirstOrDefault(t => t.SessionId == sessionId && t.IsActive(now)));

        public Task AddAsync(RefreshToken token, CancellationToken cancel)
        {
            _refreshTokens.Add(token);
            return Task.CompletedTask;
        }

        public Task RevokeSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancel)
        {
            foreach (RefreshToken token in _refreshTokens.Where(t => t.SessionId == sessionId))
            {
                token.Revoke(now);
            }
            return Task.CompletedTask;
        }

        public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancel)
        {
            foreach (RefreshToken token in _refreshTokens.Where(t => t.UserId == userId))
            {
                token.Revoke(now);
            }
            return Task.CompletedTask;
        }

        Task<Tenant?> ITenantRepository.FindByIdAsync(Guid tenantId, CancellationToken cancel) =>
            Task.FromResult(_tenants.FirstOrDefault(t => t.Id == tenantId));

        public Task<IReadOnlyList<UserTenantMembership>> GetUserTenantsAsync(Guid userId, CancellationToken cancel)
        {
            List<UserTenantMembership> list = (
                from m in _memberships
                join t in _tenants on m.TenantId equals t.Id
                where m.UserId == userId
                orderby m.JoinedAt
                select new UserTenantMembership(t, m)).ToList();
            return Task.FromResult<IReadOnlyList<UserTenantMembership>>(list);
        }

        public Task<Membership?> FindMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancel) =>
            Task.FromResult(_memberships.FirstOrDefault(m => m.TenantId == tenantId && m.UserId == userId));

        public Task<IReadOnlyList<Membership>> GetMembersAsync(Guid tenantId, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<Membership>>(
                _memberships.Where(m => m.TenantId == tenantId).OrderBy(m => m.JoinedAt).ToList());

        public Task AddTenantWithOwnerAsync(Tenant tenant, Membership ownerMembership, CancellationToken cancel)
        {
            _tenants.Add(tenant);
            _memberships.Add(ownerMembership);
            return Task.CompletedTask;
        }

        public Task AddMembershipAsync(Membership membership, CancellationToken cancel)
        {
            _memberships.Add(membership);
            return Task.CompletedTask;
        }

        public void RemoveMembership(Membership membership) => _memberships.Remove(membership);

        public Task<TenantInvitation?> FindInvitationByIdAsync(Guid invitationId, CancellationToken cancel) =>
            Task.FromResult(_invitations.FirstOrDefault(i => i.Id == invitationId));

        public Task<TenantInvitation?> FindInvitationByTokenHashAsync(string tokenHash, CancellationToken cancel) =>
            Task.FromResult(_invitations.FirstOrDefault(i => i.TokenHash == tokenHash));

        public Task<IReadOnlyList<TenantInvitation>> GetPendingInvitationsAsync(
            Guid tenantId,
            DateTimeOffset now,
            CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<TenantInvitation>>(
                _invitations.Where(i => i.TenantId == tenantId && i.IsPendingValid(now)).ToList());

        public Task AddInvitationAsync(TenantInvitation invitation, CancellationToken cancel)
        {
            _invitations.Add(invitation);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancel) => Task.CompletedTask;
    }

    private sealed class NoOpSubscriptionProvisioner : ISubscriptionProvisioner
    {
        public Task<Result> ProvisionTrialSubscriptionAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancel = default) =>
            Task.FromResult(Result.Success());
    }
}
