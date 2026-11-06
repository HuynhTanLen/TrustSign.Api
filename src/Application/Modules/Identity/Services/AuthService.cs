using System.Net;
using Microsoft.Extensions.Options;
using TrustSign.Application.Modules.Notifications;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Common.Validation;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Identity;

internal sealed class AuthService(
    IUserRepository users,
    IEmailVerificationTokenRepository verificationTokens,
    IPasswordResetTokenRepository passwordResetTokens,
    IRefreshTokenRepository refreshTokens,
    ITenantRepository tenants,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer tokenIssuer,
    IEmailSender emailSender,
    IOptions<JwtOptions> jwtOptions,
    IOptions<SmtpOptions> smtpOptions,
    TimeProvider timeProvider) : IAuthService
{
    public const int MinPasswordLength = 8;
    public static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromMinutes(30);

    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

    public async Task<Result<RegisterResultDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancel)
    {
        if (!request.AcceptTerms)
        {
            return IdentityErrors.TermsMustBeAccepted;
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && !PhoneValidator.IsValid(request.PhoneNumber))
        {
            return IdentityErrors.InvalidPhoneNumber;
        }

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 100)
        {
            return IdentityErrors.InvalidFullName;
        }

        if (!EmailValidator.IsValid(request.Email))
        {
            return IdentityErrors.InvalidEmail;
        }

        if (!IsAcceptablePassword(request.Password))
        {
            return IdentityErrors.WeakPassword;
        }

        User? existing = await users.FindByEmailAsync(request.Email, cancel);
        if (existing is not null)
        {
            return IdentityErrors.EmailAlreadyExists;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        string passwordHash = passwordHasher.Hash(request.Password);
        User user = User.Register(
            request.Email,
            request.FullName,
            passwordHash,
            now,
            request.PhoneNumber,
            now,
            User.DefaultTermsVersion);
        await users.AddAsync(user, cancel);

        await IssueAndSendVerificationEmailAsync(user, now, cancel);

        return new RegisterResultDto(
            "Đăng ký tài khoản thành công. Vui lòng kiểm tra email để xác minh tài khoản.",
            user.Email);
    }

    public async Task<Result<ActionMessageDto>> VerifyEmailAsync(string rawToken, CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return IdentityErrors.VerificationTokenInvalid;
        }

        string tokenHash = TokenGenerator.HashToken(rawToken.Trim());
        EmailVerificationToken? token = await verificationTokens.FindByTokenHashAsync(tokenHash, cancel);
        if (token is null)
        {
            return IdentityErrors.VerificationTokenInvalid;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Result consumeResult = token.Consume(now);
        if (consumeResult.IsFailure)
        {
            return consumeResult.Error;
        }

        User? user = await users.FindByIdAsync(token.UserId, cancel);
        if (user is null)
        {
            return IdentityErrors.VerificationTokenInvalid;
        }

        user.MarkEmailVerified();
        await verificationTokens.SaveChangesAsync(cancel);

        return new ActionMessageDto(true, "Xác minh tài khoản thành công.");
    }

    public async Task<Result<ActionMessageDto>> ResendVerificationAsync(string email, CancellationToken cancel)
    {
        if (EmailValidator.IsValid(email))
        {
            User? user = await users.FindByEmailAsync(email, cancel);
            if (user is not null && user.Status == UserStatus.Pending)
            {
                DateTimeOffset now = timeProvider.GetUtcNow();
                await IssueAndSendVerificationEmailAsync(user, now, cancel);
            }
        }

        return new ActionMessageDto(true, "Nếu tài khoản tồn tại và chưa xác minh, email xác minh mới đã được gửi.");
    }

    public async Task<Result<AuthSessionResultDto>> LoginAsync(LoginRequestDto request, CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return IdentityErrors.InvalidCredentials;
        }

        User? user = await users.FindByEmailAsync(request.Email, cancel);
        if (user is null)
        {
            return IdentityErrors.InvalidCredentials;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Result canLogin = user.CanLogin(now);
        if (canLogin.IsFailure)
        {
            return canLogin.Error;
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.RecordFailedLogin(now);
            await users.SaveChangesAsync(cancel);

            if (user.LockedUntil is not null && now < user.LockedUntil)
            {
                return IdentityErrors.AccountLocked;
            }

            return IdentityErrors.InvalidCredentials;
        }

        user.RecordSuccessfulLogin(now);

        IReadOnlyList<UserTenantMembership> memberships = await tenants.GetUserTenantsAsync(user.Id, cancel);
        UserTenantMembership? active = SelectActiveTenant(memberships, preferredTenantId: null);

        (string rawRefresh, string refreshHash) = TokenGenerator.GeneratePair();
        RefreshToken refresh = RefreshToken.IssueNewSession(
            user.Id,
            active?.Tenant.Id,
            refreshHash,
            now,
            TimeSpan.FromDays(_jwtOptions.RefreshTokenDays));

        await refreshTokens.AddAsync(refresh, cancel);

        return IssueSessionResult(user, refresh.SessionId, rawRefresh, refresh.ExpiresAt, memberships, active);
    }

    public async Task<Result<AuthSessionResultDto>> RefreshAsync(string? rawRefreshToken, CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return IdentityErrors.RefreshTokenInvalid;
        }

        string tokenHash = TokenGenerator.HashToken(rawRefreshToken.Trim());
        RefreshToken? existing = await refreshTokens.FindByTokenHashAsync(tokenHash, cancel);
        if (existing is null)
        {
            return IdentityErrors.RefreshTokenInvalid;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        // TC11: Phát hiện dùng lại refresh token đã được xoay vòng -> thu hồi toàn bộ phiên đăng nhập.
        if (existing.IsConsumed)
        {
            await refreshTokens.RevokeSessionAsync(existing.SessionId, now, cancel);
            return IdentityErrors.RefreshTokenReused;
        }

        if (!existing.IsActive(now))
        {
            return IdentityErrors.RefreshTokenInvalid;
        }

        User? user = await users.FindByIdAsync(existing.UserId, cancel);
        if (user is null)
        {
            return IdentityErrors.SessionInvalid;
        }

        Result canLogin = user.CanLogin(now);
        if (canLogin.IsFailure)
        {
            await refreshTokens.RevokeSessionAsync(existing.SessionId, now, cancel);
            return canLogin.Error;
        }

        IReadOnlyList<UserTenantMembership> memberships = await tenants.GetUserTenantsAsync(user.Id, cancel);
        UserTenantMembership? active = SelectActiveTenant(memberships, existing.ActiveTenantId);

        (string nextRaw, string nextHash) = TokenGenerator.GeneratePair();
        RefreshToken rotated = existing.Rotate(
            nextHash,
            active?.Tenant.Id,
            now,
            TimeSpan.FromDays(_jwtOptions.RefreshTokenDays));

        await refreshTokens.AddAsync(rotated, cancel);

        return IssueSessionResult(user, rotated.SessionId, nextRaw, rotated.ExpiresAt, memberships, active);
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return;
        }

        string tokenHash = TokenGenerator.HashToken(rawRefreshToken.Trim());
        RefreshToken? existing = await refreshTokens.FindByTokenHashAsync(tokenHash, cancel);
        if (existing is not null)
        {
            await refreshTokens.RevokeSessionAsync(existing.SessionId, timeProvider.GetUtcNow(), cancel);
        }
    }

    public async Task<Result<ActionMessageDto>> ForgotPasswordAsync(string email, CancellationToken cancel)
    {
        if (EmailValidator.IsValid(email))
        {
            User? user = await users.FindByEmailAsync(email, cancel);
            if (user is not null && user.Status == UserStatus.Active)
            {
                DateTimeOffset now = timeProvider.GetUtcNow();

                // Chỉ link mới nhất còn hiệu lực, link cũ (nếu có) bị vô hiệu hóa.
                await passwordResetTokens.InvalidateAllForUserAsync(user.Id, now, cancel);

                (string rawToken, string tokenHash) = TokenGenerator.GeneratePair();
                PasswordResetToken reset = PasswordResetToken.Create(user.Id, tokenHash, now, PasswordResetTokenLifetime);
                await passwordResetTokens.AddAsync(reset, cancel);

                await SendPasswordResetEmailAsync(user, rawToken, cancel);
            }
        }

        return new ActionMessageDto(
            true,
            "Nếu email thuộc một tài khoản đang hoạt động, hướng dẫn đặt lại mật khẩu đã được gửi tới hộp thư.");
    }

    public async Task<Result<ActionMessageDto>> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return IdentityErrors.PasswordResetTokenInvalid;
        }

        if (!IsAcceptablePassword(request.NewPassword))
        {
            return IdentityErrors.WeakPassword;
        }

        string tokenHash = TokenGenerator.HashToken(request.Token.Trim());
        PasswordResetToken? token = await passwordResetTokens.FindByTokenHashAsync(tokenHash, cancel);
        if (token is null)
        {
            return IdentityErrors.PasswordResetTokenInvalid;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!token.IsUsable(now))
        {
            return IdentityErrors.PasswordResetTokenInvalid;
        }

        User? user = await users.FindByIdAsync(token.UserId, cancel);
        if (user is null || user.Status != UserStatus.Active)
        {
            return IdentityErrors.PasswordResetTokenInvalid;
        }

        Result consume = token.Consume(now);
        if (consume.IsFailure)
        {
            return consume.Error;
        }

        user.ResetPassword(passwordHasher.Hash(request.NewPassword));
        await users.SaveChangesAsync(cancel);

        // Đổi mật khẩu bằng email thì mọi phiên đăng nhập cũ (có thể của kẻ gian) đều bị thu hồi.
        await refreshTokens.RevokeAllForUserAsync(user.Id, now, cancel);
        await passwordResetTokens.InvalidateAllForUserAsync(user.Id, now, cancel);

        return new ActionMessageDto(true, "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.");
    }

    public async Task<Result<ActionMessageDto>> ChangePasswordAsync(
        Guid userId,
        Guid? currentSessionId,
        ChangePasswordRequestDto request,
        CancellationToken cancel)
    {
        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return IdentityErrors.SessionInvalid;
        }

        if (string.IsNullOrEmpty(request.CurrentPassword) ||
            !passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return IdentityErrors.CurrentPasswordIncorrect;
        }

        if (!IsAcceptablePassword(request.NewPassword))
        {
            return IdentityErrors.WeakPassword;
        }

        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            return IdentityErrors.PasswordUnchanged;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        user.ChangePassword(passwordHasher.Hash(request.NewPassword));
        await users.SaveChangesAsync(cancel);

        await refreshTokens.RevokeOtherSessionsAsync(user.Id, currentSessionId, now, cancel);

        return new ActionMessageDto(true, "Đổi mật khẩu thành công. Các phiên đăng nhập khác đã bị đăng xuất.");
    }

    private static bool IsAcceptablePassword(string? password) =>
        !string.IsNullOrWhiteSpace(password) &&
        password.Length >= MinPasswordLength &&
        password.Any(char.IsLetter) &&
        password.Any(char.IsDigit);

    private async Task SendPasswordResetEmailAsync(User user, string rawToken, CancellationToken cancel)
    {
        string baseUrl = _smtpOptions.FrontendBaseUrl.TrimEnd('/');
        string resetUrl = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        string safeName = WebUtility.HtmlEncode(user.FullName);
        int minutes = (int)PasswordResetTokenLifetime.TotalMinutes;

        string html = $"""
            <div style="font-family:'Segoe UI',Roboto,Arial,sans-serif;max-width:560px;margin:0 auto;color:#1f2328;line-height:1.6;">
              <h2 style="color:#1b3a5c;margin-bottom:12px;">Chữ Tín (TrustSign) – Đặt lại mật khẩu</h2>
              <p>Xin chào <strong>{safeName}</strong>,</p>
              <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Nhấn vào nút bên dưới để đặt mật khẩu mới:</p>
              <p style="margin:24px 0;">
                <a href="{resetUrl}" style="background:#1b3a5c;color:#ffffff;text-decoration:none;padding:10px 20px;border-radius:4px;display:inline-block;font-weight:600;">
                  Đặt lại mật khẩu
                </a>
              </p>
              <p style="font-size:13px;color:#5f6b7a;">Hoặc mở trực tiếp đường dẫn: <a href="{resetUrl}">{resetUrl}</a></p>
              <p style="font-size:12px;color:#5f6b7a;">Liên kết có hiệu lực trong {minutes} phút và chỉ sử dụng một lần. Nếu bạn không yêu cầu, hãy bỏ qua email này; mật khẩu hiện tại vẫn giữ nguyên.</p>
            </div>
            """;

        await emailSender.SendAsync(
            new EmailMessage(user.Email, "[Chữ Tín] Đặt lại mật khẩu tài khoản", html),
            cancel);
    }

    private AuthSessionResultDto IssueSessionResult(
        User user,
        Guid sessionId,
        string rawRefresh,
        DateTimeOffset refreshExpiresAt,
        IReadOnlyList<UserTenantMembership> memberships,
        UserTenantMembership? active)
    {
        TenantClaimContext? tenantClaim = active is null
            ? null
            : new TenantClaimContext(active.Tenant.Id, active.Membership.Role, active.Tenant.Status);

        AccessTokenDto access = tokenIssuer.Issue(user.Id, sessionId, user.Role, tenantClaim);
        UserProfileDto profile = user.ToProfileDto(memberships, active?.Tenant.Id);

        return new AuthSessionResultDto(
            access.AccessToken,
            access.ExpiresAt,
            rawRefresh,
            refreshExpiresAt,
            profile);
    }

    private async Task IssueAndSendVerificationEmailAsync(User user, DateTimeOffset now, CancellationToken cancel)
    {
        (string rawToken, string tokenHash) = TokenGenerator.GeneratePair();
        EmailVerificationToken verification = EmailVerificationToken.Create(
            user.Id,
            tokenHash,
            now,
            TimeSpan.FromHours(_jwtOptions.VerificationTokenHours));

        await verificationTokens.AddAsync(verification, cancel);

        string baseUrl = _smtpOptions.FrontendBaseUrl.TrimEnd('/');
        string verifyUrl = $"{baseUrl}/verify-email?token={Uri.EscapeDataString(rawToken)}";
        string safeName = WebUtility.HtmlEncode(user.FullName);

        string html = $"""
            <div style="font-family:'Segoe UI',Roboto,Arial,sans-serif;max-width:560px;margin:0 auto;color:#1f2328;line-height:1.6;">
              <h2 style="color:#1f5fa8;margin-bottom:12px;">Chữ Tín (TrustSign) – Xác minh tài khoản</h2>
              <p>Xin chào <strong>{safeName}</strong>,</p>
              <p>Cảm ơn bạn đã đăng ký tài khoản trên hệ thống hợp đồng điện tử Chữ Tín. Vui lòng nhấn vào liên kết bên dưới để kích hoạt tài khoản:</p>
              <p style="margin:24px 0;">
                <a href="{verifyUrl}" style="background:#1f5fa8;color:#ffffff;text-decoration:none;padding:10px 20px;border-radius:4px;display:inline-block;font-weight:600;">
                  Xác minh tài khoản
                </a>
              </p>
              <p style="font-size:13px;color:#5f6b7a;">Hoặc mở trực tiếp đường dẫn: <a href="{verifyUrl}">{verifyUrl}</a></p>
              <p style="font-size:12px;color:#5f6b7a;">Liên kết có hiệu lực trong {_jwtOptions.VerificationTokenHours} giờ và chỉ sử dụng một lần.</p>
            </div>
            """;

        await emailSender.SendAsync(
            new EmailMessage(user.Email, "[Chữ Tín] Xác minh địa chỉ email tài khoản", html),
            cancel);
    }

    private static UserTenantMembership? SelectActiveTenant(
        IReadOnlyList<UserTenantMembership> memberships,
        Guid? preferredTenantId)
    {
        if (preferredTenantId is not null)
        {
            UserTenantMembership? preferred = memberships.FirstOrDefault(item => item.Tenant.Id == preferredTenantId);
            if (preferred is not null)
            {
                return preferred;
            }
        }

        return memberships.FirstOrDefault(item => item.Tenant.Status == TenantStatus.Active)
            ?? memberships.FirstOrDefault();
    }
}
