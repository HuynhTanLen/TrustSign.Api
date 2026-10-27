namespace TrustSign.Application.Modules.Identity;

public sealed record AccessTokenDto(string AccessToken, DateTimeOffset ExpiresAt);
