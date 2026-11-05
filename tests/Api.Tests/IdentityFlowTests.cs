using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using TrustSign.Api.Security;
using TrustSign.Application.Modules.Audit;
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
            new NoOpAuditLogger(),
            smtpOptions,
            _clock);
    }

    [Fact]
    public async Task Register_verify_once_and_login_flow_succeeds()
    {
        Result<RegisterResultDto> reg = await _auth.RegisterAsync(
            new RegisterRequestDto("Nguyễn Văn An", "An.Nguyen@TrustSign.vn", "TrustSign@2026", AcceptTerms: true),
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

    [Fact]
    public async Task Register_without_accepting_terms_fails_with_validation_error()
    {
        Result<RegisterResultDto> reg = await _auth.RegisterAsync(
            new RegisterRequestDto("Người Dùng Mới", "noterms@trustsign.vn", "TrustSign@2026", AcceptTerms: false),
            CancellationToken.None);

        Assert.True(reg.IsFailure);
        Assert.Equal(ErrorType.Validation, reg.Error.Type);
        Assert.Equal(IdentityErrors.TermsMustBeAccepted, reg.Error);
    }

    [Theory]
    [InlineData("0912345678", true)]
    [InlineData("+84912345678", true)]
    [InlineData("12345", false)]
    [InlineData("09123456789", false)]
    public async Task Register_phone_number_validation_works(string phone, bool isValid)
    {
        string email = $"phone_{Guid.NewGuid():N}@trustsign.vn";
        Result<RegisterResultDto> reg = await _auth.RegisterAsync(
            new RegisterRequestDto("Người Có SĐT", email, "TrustSign@2026", AcceptTerms: true, PhoneNumber: phone),
            CancellationToken.None);

        if (isValid)
        {
            Assert.True(reg.IsSuccess);
        }
        else
        {
            Assert.True(reg.IsFailure);
            Assert.Equal(IdentityErrors.InvalidPhoneNumber, reg.Error);
        }
    }

    [Theory]
    [InlineData("0101234567", true)]
    [InlineData("0101234567-001", true)]
    [InlineData("123", false)]
    [InlineData("0101234567890", false)]
    [InlineData("abc", false)]
    public async Task Create_tenant_validates_tax_code(string taxCode, bool isValid)
    {
        AuthSessionResultDto owner = await RegisterVerifyAndLoginAsync($"tax_{Guid.NewGuid():N}@company.vn", "Chủ DN Thuế");
        Guid sessionId = ParseSessionId(owner.AccessToken);

        Result<TenantSessionResponseDto> create = await _tenants.CreateTenantAsync(
            owner.User.Id,
            sessionId,
            new CreateTenantRequestDto("Công ty Thuế", taxCode, "Hà Nội", "tax@company.vn"),
            CancellationToken.None);

        if (isValid)
        {
            Assert.True(create.IsSuccess);
            Assert.Equal(taxCode.Trim(), create.Value.Tenant.TaxCode);
        }
        else
        {
            Assert.True(create.IsFailure);
            Assert.Equal(IdentityErrors.InvalidTaxCode, create.Error);
        }
    }

    [Fact]
    public async Task Non_owner_calling_update_current_tenant_is_forbidden_403()
    {
        AuthSessionResultDto owner = await RegisterVerifyAndLoginAsync("owner_patch@company.vn", "Chủ DN");
        Result<TenantSessionResponseDto> tenantRes = await _tenants.CreateTenantAsync(
            owner.User.Id,
            ParseSessionId(owner.AccessToken),
            new CreateTenantRequestDto("Công ty A", "0101234567", "Hà Nội", "contact@comp.vn"),
            CancellationToken.None);
        Guid tenantId = tenantRes.Value.Tenant.Id;

        // Mời nhân viên và nhân viên chấp nhận
        await _tenants.InviteMemberAsync(
            tenantId,
            owner.User.Id,
            new CreateInvitationRequestDto("employee@company.vn", nameof(TenantRole.Employee)),
            CancellationToken.None);
        string inviteToken = ExtractInvitationToken(_emails.LastSent!.HtmlBody);

        AuthSessionResultDto employee = await RegisterVerifyAndLoginAsync("employee@company.vn", "Nhân viên B");
        await _tenants.AcceptInvitationAsync(
            employee.User.Id,
            ParseSessionId(employee.AccessToken),
            inviteToken,
            CancellationToken.None);

        // Nhân viên gọi UpdateCurrentTenantAsync -> 403 Forbidden (TenantAccessDenied)
        Result<TenantDto> nonOwnerUpdate = await _tenants.UpdateCurrentTenantAsync(
            tenantId,
            employee.User.Id,
            new UpdateTenantRequestDto("Tên Mới", "0101234567", "Đà Nẵng", "new@comp.vn", "0901234567", "Nguyễn Đại Diện", "Giám đốc"),
            CancellationToken.None);

        Assert.True(nonOwnerUpdate.IsFailure);
        Assert.Equal(ErrorType.Forbidden, nonOwnerUpdate.Error.Type);
        Assert.Equal(IdentityErrors.TenantAccessDenied, nonOwnerUpdate.Error);

        // Owner gọi UpdateCurrentTenantAsync -> 200 OK
        Result<TenantDto> ownerUpdate = await _tenants.UpdateCurrentTenantAsync(
            tenantId,
            owner.User.Id,
            new UpdateTenantRequestDto("Tên Đã Đổi", "0101234567-001", "TP.HCM", "owner_new@comp.vn", "0909888999", "Nguyễn Văn Chủ", "Tổng Giám đốc"),
            CancellationToken.None);

        Assert.True(ownerUpdate.IsSuccess);
        Assert.Equal("Tên Đã Đổi", ownerUpdate.Value.Name);
        Assert.Equal("0101234567-001", ownerUpdate.Value.TaxCode);
        Assert.Equal("Nguyễn Văn Chủ", ownerUpdate.Value.RepresentativeName);
        Assert.Equal("Tổng Giám đốc", ownerUpdate.Value.RepresentativeTitle);
        Assert.Equal("0909888999", ownerUpdate.Value.Phone);

        // Gọi GetCurrentTenantAsync trả về TenantDto đầy đủ
        Result<TenantDto> current = await _tenants.GetCurrentTenantAsync(tenantId, owner.User.Id, CancellationToken.None);
        Assert.True(current.IsSuccess);
        Assert.Equal("Tên Đã Đổi", current.Value.Name);
        Assert.Equal("Nguyễn Văn Chủ", current.Value.RepresentativeName);
    }

    [Fact]
    public async Task Reset_password_allows_login_with_new_password_and_rejects_old_password()
    {
        AuthSessionResultDto session = await RegisterVerifyAndLoginAsync("reset@trustsign.vn", "Người Quên");

        Result<ActionMessageDto> forgot = await _auth.ForgotPasswordAsync("Reset@TrustSign.vn", CancellationToken.None);
        Assert.True(forgot.IsSuccess);
        string resetToken = ExtractResetToken(_emails.LastSent!.HtmlBody);

        Result<ActionMessageDto> reset = await _auth.ResetPasswordAsync(
            new ResetPasswordRequestDto(resetToken, "MatKhauMoi@2026"),
            CancellationToken.None);
        Assert.True(reset.IsSuccess);

        Result<AuthSessionResultDto> loginNew = await _auth.LoginAsync(
            new LoginRequestDto("reset@trustsign.vn", "MatKhauMoi@2026"),
            CancellationToken.None);
        Assert.True(loginNew.IsSuccess);

        Result<AuthSessionResultDto> loginOld = await _auth.LoginAsync(
            new LoginRequestDto("reset@trustsign.vn", "TrustSign@2026"),
            CancellationToken.None);
        Assert.True(loginOld.IsFailure);
        Assert.Equal(IdentityErrors.InvalidCredentials, loginOld.Error);

        // Mọi refresh token phát ra trước khi đặt lại mật khẩu đều bị thu hồi.
        Result<AuthSessionResultDto> oldRefresh = await _auth.RefreshAsync(session.RawRefreshToken, CancellationToken.None);
        Assert.True(oldRefresh.IsFailure);
    }

    [Fact]
    public async Task Reset_password_token_can_only_be_used_once()
    {
        await RegisterVerifyAndLoginAsync("once@trustsign.vn", "Một Lần");
        await _auth.ForgotPasswordAsync("once@trustsign.vn", CancellationToken.None);
        string resetToken = ExtractResetToken(_emails.LastSent!.HtmlBody);

        Result<ActionMessageDto> first = await _auth.ResetPasswordAsync(
            new ResetPasswordRequestDto(resetToken, "MatKhauMoi@2026"),
            CancellationToken.None);
        Result<ActionMessageDto> second = await _auth.ResetPasswordAsync(
            new ResetPasswordRequestDto(resetToken, "MatKhauKhac@2026"),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal(IdentityErrors.PasswordResetTokenInvalid, second.Error);

        // Lần dùng thứ hai không được làm đổi mật khẩu.
        Result<AuthSessionResultDto> login = await _auth.LoginAsync(
            new LoginRequestDto("once@trustsign.vn", "MatKhauMoi@2026"),
            CancellationToken.None);
        Assert.True(login.IsSuccess);
    }

    [Fact]
    public async Task Reset_password_token_expires_after_30_minutes()
    {
        await RegisterVerifyAndLoginAsync("expired@trustsign.vn", "Hết Hạn");
        await _auth.ForgotPasswordAsync("expired@trustsign.vn", CancellationToken.None);
        string resetToken = ExtractResetToken(_emails.LastSent!.HtmlBody);

        _clock.Current += TimeSpan.FromMinutes(30);

        Result<ActionMessageDto> reset = await _auth.ResetPasswordAsync(
            new ResetPasswordRequestDto(resetToken, "MatKhauMoi@2026"),
            CancellationToken.None);

        Assert.True(reset.IsFailure);
        Assert.Equal(IdentityErrors.PasswordResetTokenInvalid, reset.Error);

        Result<AuthSessionResultDto> loginOld = await _auth.LoginAsync(
            new LoginRequestDto("expired@trustsign.vn", "TrustSign@2026"),
            CancellationToken.None);
        Assert.True(loginOld.IsSuccess);
    }

    [Fact]
    public async Task Reset_password_with_wrong_token_is_rejected()
    {
        await RegisterVerifyAndLoginAsync("wrong@trustsign.vn", "Sai Token");
        await _auth.ForgotPasswordAsync("wrong@trustsign.vn", CancellationToken.None);

        Result<ActionMessageDto> reset = await _auth.ResetPasswordAsync(
            new ResetPasswordRequestDto("token-khong-ton-tai", "MatKhauMoi@2026"),
            CancellationToken.None);

        Assert.True(reset.IsFailure);
        Assert.Equal(IdentityErrors.PasswordResetTokenInvalid, reset.Error);
    }

    [Fact]
    public async Task Reset_password_rejects_short_password()
    {
        await RegisterVerifyAndLoginAsync("short@trustsign.vn", "Mật Khẩu Ngắn");
        await _auth.ForgotPasswordAsync("short@trustsign.vn", CancellationToken.None);
        string resetToken = ExtractResetToken(_emails.LastSent!.HtmlBody);

        Result<ActionMessageDto> reset = await _auth.ResetPasswordAsync(
            new ResetPasswordRequestDto(resetToken, "1234567"),
            CancellationToken.None);

        Assert.True(reset.IsFailure);
        Assert.Equal(IdentityErrors.WeakPassword, reset.Error);
    }

    [Fact]
    public async Task Forgot_password_does_not_reveal_unknown_or_unverified_emails()
    {
        await _auth.RegisterAsync(
            new RegisterRequestDto("Chưa Xác Minh", "pending@trustsign.vn", "TrustSign@2026", AcceptTerms: true),
            CancellationToken.None);
        EmailMessage? before = _emails.LastSent;

        Result<ActionMessageDto> unknown = await _auth.ForgotPasswordAsync("nobody@trustsign.vn", CancellationToken.None);
        Result<ActionMessageDto> pending = await _auth.ForgotPasswordAsync("pending@trustsign.vn", CancellationToken.None);
        Result<ActionMessageDto> malformed = await _auth.ForgotPasswordAsync("not-an-email", CancellationToken.None);

        Assert.True(unknown.IsSuccess);
        Assert.True(pending.IsSuccess);
        Assert.True(malformed.IsSuccess);
        Assert.Equal(unknown.Value.Message, pending.Value.Message);
        Assert.Same(before, _emails.LastSent);
    }

    [Fact]
    public async Task Reset_password_clears_login_lockout()
    {
        await RegisterVerifyAndLoginAsync("locked@trustsign.vn", "Bị Khóa");
        for (int i = 0; i < 5; i++)
        {
            await _auth.LoginAsync(new LoginRequestDto("locked@trustsign.vn", "sai-mat-khau"), CancellationToken.None);
        }

        Result<AuthSessionResultDto> whileLocked = await _auth.LoginAsync(
            new LoginRequestDto("locked@trustsign.vn", "TrustSign@2026"),
            CancellationToken.None);
        Assert.Equal(IdentityErrors.AccountLocked, whileLocked.Error);

        await _auth.ForgotPasswordAsync("locked@trustsign.vn", CancellationToken.None);
        string resetToken = ExtractResetToken(_emails.LastSent!.HtmlBody);
        Result<ActionMessageDto> reset = await _auth.ResetPasswordAsync(
            new ResetPasswordRequestDto(resetToken, "MatKhauMoi@2026"),
            CancellationToken.None);
        Assert.True(reset.IsSuccess);

        Result<AuthSessionResultDto> login = await _auth.LoginAsync(
            new LoginRequestDto("locked@trustsign.vn", "MatKhauMoi@2026"),
            CancellationToken.None);
        Assert.True(login.IsSuccess);
    }

    [Fact]
    public async Task Change_password_requires_current_password_and_revokes_other_sessions()
    {
        AuthSessionResultDto current = await RegisterVerifyAndLoginAsync("change@trustsign.vn", "Đổi Mật Khẩu");
        Result<AuthSessionResultDto> other = await _auth.LoginAsync(
            new LoginRequestDto("change@trustsign.vn", "TrustSign@2026"),
            CancellationToken.None);
        Assert.True(other.IsSuccess);

        Result<ActionMessageDto> wrongCurrent = await _auth.ChangePasswordAsync(
            current.User.Id,
            ParseSessionId(current.AccessToken),
            new ChangePasswordRequestDto("sai-mat-khau", "MatKhauMoi@2026"),
            CancellationToken.None);
        Assert.True(wrongCurrent.IsFailure);
        Assert.Equal(IdentityErrors.CurrentPasswordIncorrect, wrongCurrent.Error);

        Result<ActionMessageDto> changed = await _auth.ChangePasswordAsync(
            current.User.Id,
            ParseSessionId(current.AccessToken),
            new ChangePasswordRequestDto("TrustSign@2026", "MatKhauMoi@2026"),
            CancellationToken.None);
        Assert.True(changed.IsSuccess);

        // Phiên hiện tại vẫn làm mới được, phiên khác bị thu hồi.
        Assert.True((await _auth.RefreshAsync(current.RawRefreshToken, CancellationToken.None)).IsSuccess);
        Assert.True((await _auth.RefreshAsync(other.Value.RawRefreshToken, CancellationToken.None)).IsFailure);

        Assert.True((await _auth.LoginAsync(
            new LoginRequestDto("change@trustsign.vn", "MatKhauMoi@2026"),
            CancellationToken.None)).IsSuccess);
        Assert.Equal(
            IdentityErrors.InvalidCredentials,
            (await _auth.LoginAsync(new LoginRequestDto("change@trustsign.vn", "TrustSign@2026"), CancellationToken.None)).Error);
    }

    private static string ExtractResetToken(string html)
    {
        Match match = Regex.Match(html, @"reset-password\?token=([^""&<]+)");
        Assert.True(match.Success, "Không tìm thấy link reset-password trong nội dung email.");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private async Task<AuthSessionResultDto> RegisterVerifyAndLoginAsync(string email, string fullName)
    {
        await _auth.RegisterAsync(new RegisterRequestDto(fullName, email, "TrustSign@2026", AcceptTerms: true), CancellationToken.None);
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
        IPasswordResetTokenRepository,
        IRefreshTokenRepository,
        ITenantRepository
    {
        private readonly List<User> _users = [];
        private readonly List<EmailVerificationToken> _verificationTokens = [];
        private readonly List<PasswordResetToken> _passwordResetTokens = [];
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

        public Task RevokeOtherSessionsAsync(Guid userId, Guid? keepSessionId, DateTimeOffset now, CancellationToken cancel)
        {
            foreach (RefreshToken token in _refreshTokens.Where(t => t.UserId == userId && t.SessionId != keepSessionId))
            {
                token.Revoke(now);
            }
            return Task.CompletedTask;
        }

        Task<PasswordResetToken?> IPasswordResetTokenRepository.FindByTokenHashAsync(string tokenHash, CancellationToken cancel) =>
            Task.FromResult(_passwordResetTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

        public Task AddAsync(PasswordResetToken token, CancellationToken cancel)
        {
            _passwordResetTokens.Add(token);
            return Task.CompletedTask;
        }

        public Task InvalidateAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancel)
        {
            foreach (PasswordResetToken token in _passwordResetTokens.Where(t => t.UserId == userId && t.IsUsable(now)))
            {
                token.Consume(now);
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

    private sealed class NoOpAuditLogger : IAuditLogger
    {
        public Task LogAsync(
            Guid? tenantId,
            Guid? actorUserId,
            string action,
            string resourceType,
            string resourceId,
            object? payload = null,
            CancellationToken cancel = default) => Task.CompletedTask;
    }
}
