using TrustSign.Common;

namespace TrustSign.Application.Modules.Identity;

public interface IUserService
{
    const int MaxAvatarBytes = 128 * 1024;

    Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, Guid? activeTenantId, CancellationToken cancel);

    Task<Result<UserProfileDto>> UpdateProfileAsync(
        Guid userId,
        Guid? activeTenantId,
        string fullName,
        string? phoneNumber,
        string? jobTitle,
        CancellationToken cancel);

    /// <summary>Thay ảnh đại diện; client đã thu nhỏ, máy chủ kiểm tra lại loại file (JPEG/PNG/WebP) và kích thước.</summary>
    Task<Result<UserProfileDto>> SetAvatarAsync(Guid userId, Guid? activeTenantId, ReadOnlyMemory<byte> image, CancellationToken cancel);

    Task<Result<UserProfileDto>> RemoveAvatarAsync(Guid userId, Guid? activeTenantId, CancellationToken cancel);

    /// <summary>Đọc ảnh bằng token ký trong URL (thẻ img không gửi được Bearer).</summary>
    Task<Result<byte[]>> GetAvatarByTokenAsync(Guid userId, string token, CancellationToken cancel);
}
