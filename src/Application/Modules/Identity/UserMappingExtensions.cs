using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Application.Modules.Identity;

internal static class UserMappingExtensions
{
    public static UserProfileDto ToProfileDto(this User user) =>
        new(user.Id, user.Email, user.FullName, user.Role.ToString());
}
