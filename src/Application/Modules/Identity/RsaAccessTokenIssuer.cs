using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TrustSign.Common;
using TrustSign.Common.Enums;

namespace TrustSign.Application.Modules.Identity;

/// <summary>Cấp access token ký RS256 bằng khóa đang hoạt động của <see cref="SigningKeyRing"/>.</summary>
public sealed class RsaAccessTokenIssuer(SigningKeyRing keyRing, IOptions<JwtOptions> options, TimeProvider timeProvider)
    : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly JwtOptions _options = options.Value;

    public AccessTokenDto Issue(Guid userId, Guid sessionId, UserRole role)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        string token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtClaims.Subject, userId.ToString("D")),
                new Claim(JwtClaims.Session, sessionId.ToString("D")),
                new Claim(JwtClaims.Role, role.ToString())
            ]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(keyRing.ActiveKey, SecurityAlgorithms.RsaSha256)
        });

        return new AccessTokenDto(token, expiresAt);
    }
}
