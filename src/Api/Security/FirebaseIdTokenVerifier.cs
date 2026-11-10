using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using TrustSign.Application.Modules.Identity;

namespace TrustSign.Api.Security;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    /// <summary>Để trống thì tắt đăng nhập Google: mọi token bị từ chối.</summary>
    public string ProjectId { get; set; } = string.Empty;
}

/// <summary>
/// Kiểm Firebase ID token bằng khóa công khai Google (không cần secret): chữ ký RS256, issuer/audience theo project,
/// hạn dùng, và nhà cung cấp đăng nhập phải là google.com để token email/mật khẩu của Firebase không đăng nhập được.
/// </summary>
public sealed class FirebaseIdTokenVerifier : IExternalIdentityVerifier
{
    private readonly string _projectId;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configuration;
    private readonly JsonWebTokenHandler _handler = new();

    public FirebaseIdTokenVerifier(Microsoft.Extensions.Options.IOptions<FirebaseOptions> options)
    {
        _projectId = options.Value.ProjectId.Trim();
        _configuration = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"https://securetoken.google.com/{_projectId}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());
    }

    public async Task<ExternalIdentity?> VerifyGoogleAsync(string idToken, CancellationToken cancel)
    {
        if (_projectId.Length == 0)
        {
            return null;
        }

        try
        {
            OpenIdConnectConfiguration config = await _configuration.GetConfigurationAsync(cancel);
            TokenValidationResult result = await _handler.ValidateTokenAsync(idToken, new TokenValidationParameters
            {
                ValidIssuer = $"https://securetoken.google.com/{_projectId}",
                ValidAudience = _projectId,
                IssuerSigningKeys = config.SigningKeys,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            });

            if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt)
            {
                return null;
            }

            if (!jwt.TryGetPayloadValue("firebase", out JsonElement firebase) ||
                !firebase.TryGetProperty("sign_in_provider", out JsonElement provider) ||
                provider.GetString() != "google.com")
            {
                return null;
            }

            string? email = jwt.TryGetPayloadValue("email", out string? e) ? e : null;
            bool verified = jwt.TryGetPayloadValue("email_verified", out bool v) && v;
            string? name = jwt.TryGetPayloadValue("name", out string? n) ? n : null;
            return string.IsNullOrEmpty(jwt.Subject) || string.IsNullOrEmpty(email)
                ? null
                : new ExternalIdentity(jwt.Subject, email, verified, name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Không lộ chi tiết lỗi cho người gọi; token sai và Google không truy cập được đều coi là thất bại.
            return null;
        }
    }
}
