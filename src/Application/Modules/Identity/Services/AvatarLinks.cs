using TrustSign.Application.Modules.Media;
using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Application.Modules.Identity;

/// <summary>URL ký của ảnh đại diện. Sống lâu hơn URL tài liệu vì nằm trong profile và hiện suốt phiên làm việc.</summary>
internal static class AvatarLinks
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    public static string? CreateAvatarUrl(this IMediaSigner signer, User user, DateTimeOffset now) =>
        user.AvatarKey is null
            ? null
            : signer.CreateUrl(MediaKinds.Avatars, user.Id, signer.CreateToken(MediaPurposes.Avatar, user.Id, Guid.Empty, Lifetime, now));

    public static bool TryValidateAvatarToken(this IMediaSigner signer, string token, DateTimeOffset now, out Guid userId) =>
        signer.TryValidate(MediaPurposes.Avatar, token, now, out userId, out Guid second) && second == Guid.Empty;
}
