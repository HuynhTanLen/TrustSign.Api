namespace TrustSign.Application.Modules.Identity;

public sealed record UserProfileDto(Guid Id, string Email, string FullName, string Role);
