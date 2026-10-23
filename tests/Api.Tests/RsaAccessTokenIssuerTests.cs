using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TrustSign.Api.Controllers.Identity;
using TrustSign.Api.Security;
using TrustSign.Application.Modules.Identity;
using TrustSign.Common;
using TrustSign.Common.Enums;

namespace TrustSign.Api.Tests;

public sealed class RsaAccessTokenIssuerTests
{
    private const string Issuer = "trustsign";
    private const string Audience = "trustsign-clients";

    private readonly SigningKeyRing _keyRing = SigningKeyRing.CreateEphemeral();
    private readonly RsaAccessTokenIssuer _issuer;

    public RsaAccessTokenIssuerTests()
    {
        _issuer = CreateIssuer(_keyRing);
    }

    [Fact]
    public async Task Token_is_accepted_by_a_verifier_that_only_has_the_published_jwks()
    {
        Guid userId = Guid.NewGuid();
        Guid sessionId = Guid.NewGuid();

        AccessTokenDto token = _issuer.Issue(userId, sessionId, UserRole.Admin);
        TokenValidationResult result = await ValidateWithPublishedJwksAsync(token.AccessToken, Audience);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(userId.ToString("D"), result.Claims[JwtClaims.Subject]);
        Assert.Equal(sessionId.ToString("D"), result.Claims[JwtClaims.Session]);
        Assert.Equal("Admin", result.Claims[JwtClaims.Role]);
    }

    [Fact]
    public async Task Token_is_rejected_for_another_audience()
    {
        AccessTokenDto token = _issuer.Issue(Guid.NewGuid(), Guid.NewGuid(), UserRole.User);

        TokenValidationResult result = await ValidateWithPublishedJwksAsync(token.AccessToken, audience: "another-system");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Token_signed_by_an_unknown_key_is_rejected()
    {
        RsaAccessTokenIssuer attacker = CreateIssuer(SigningKeyRing.CreateEphemeral());
        AccessTokenDto forged = attacker.Issue(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin);

        TokenValidationResult result = await ValidateWithPublishedJwksAsync(forged.AccessToken, Audience);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Published_jwks_never_contains_private_key_material()
    {
        using JsonDocument jwks = JsonDocument.Parse(PublishedJwksJson());

        JsonElement key = Assert.Single(jwks.RootElement.GetProperty("keys").EnumerateArray());
        string[] fields = [.. key.EnumerateObject().Select(property => property.Name)];
        Assert.Equal(["kty", "kid", "use", "alg", "n", "e"], fields);
    }

    private static RsaAccessTokenIssuer CreateIssuer(SigningKeyRing keyRing)
    {
        JwtOptions options = new() { Issuer = Issuer, Audience = Audience, AccessTokenMinutes = 15, PrivateKeyPath = "unused" };
        return new RsaAccessTokenIssuer(keyRing, Microsoft.Extensions.Options.Options.Create(options), TimeProvider.System);
    }

    /// <summary>Kiểm tra token như một bên ngoài: chỉ biết tài liệu JWKS ứng dụng công bố.</summary>
    private Task<TokenValidationResult> ValidateWithPublishedJwksAsync(string token, string audience)
    {
        TokenValidationParameters parameters = JwtBearerServiceCollectionExtensions.CreateValidationParameters(Issuer, audience);
        parameters.IssuerSigningKeys = new JsonWebKeySet(PublishedJwksJson()).GetSigningKeys();
        return new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
    }

    private string PublishedJwksJson()
    {
        OkObjectResult response = Assert.IsType<OkObjectResult>(new JwksController(_keyRing).Get());
        return JsonSerializer.Serialize(response.Value);
    }
}
