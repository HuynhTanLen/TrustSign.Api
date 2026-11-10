using TrustSign.Application.Modules.Media;
using TrustSign.Common;
using TrustSign.Common.Errors;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Identity;

internal sealed class UserService(
    IUserRepository users,
    ITenantRepository tenants,
    IExternalLoginRepository externalLogins,
    IMediaStorage mediaStorage,
    IMediaSigner mediaSigner,
    TimeProvider timeProvider) : IUserService
{
    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, Guid? activeTenantId, CancellationToken cancel)
    {
        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return IdentityErrors.SessionInvalid;
        }

        IReadOnlyList<UserTenantMembership> memberships = await tenants.GetUserTenantsAsync(userId, cancel);
        bool googleLinked = await externalLogins.FindByUserAsync(user.Id, ExternalLogin.GoogleProvider, cancel) is not null;
        return user.ToProfileDto(memberships, activeTenantId, googleLinked, AvatarUrl(user));
    }

    public async Task<Result<UserProfileDto>> UpdateProfileAsync(
        Guid userId,
        Guid? activeTenantId,
        string fullName,
        string? phoneNumber,
        string? jobTitle,
        CancellationToken cancel)
    {
        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return IdentityErrors.SessionInvalid;
        }

        Result update = user.UpdateProfile(fullName, phoneNumber, jobTitle);
        if (update.IsFailure)
        {
            return update.Error;
        }

        await users.SaveChangesAsync(cancel);
        IReadOnlyList<UserTenantMembership> memberships = await tenants.GetUserTenantsAsync(userId, cancel);
        bool googleLinked = await externalLogins.FindByUserAsync(user.Id, ExternalLogin.GoogleProvider, cancel) is not null;
        return user.ToProfileDto(memberships, activeTenantId, googleLinked, AvatarUrl(user));
    }

    public async Task<Result<UserProfileDto>> SetAvatarAsync(
        Guid userId, Guid? activeTenantId, ReadOnlyMemory<byte> image, CancellationToken cancel)
    {
        if (image.IsEmpty || image.Length > IUserService.MaxAvatarBytes ||
            MediaContent.Detect(image.Span) is not (MediaType.Jpeg or MediaType.Png or MediaType.WebP))
        {
            return IdentityErrors.AvatarInvalid;
        }

        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return IdentityErrors.SessionInvalid;
        }

        // Khóa mới mỗi lần đổi để ảnh cũ không còn truy cập được và trình duyệt không giữ bản cũ trong cache.
        string newKey = $"users/{userId:D}/avatar-{Guid.NewGuid():N}";
        string? oldKey = user.AvatarKey;
        await mediaStorage.SaveAsync(newKey, image, cancel);
        user.SetAvatarKey(newKey);
        await users.SaveChangesAsync(cancel);
        if (oldKey is not null)
        {
            await mediaStorage.DeleteAsync(oldKey, cancel);
        }

        return await GetProfileAsync(userId, activeTenantId, cancel);
    }

    public async Task<Result<UserProfileDto>> RemoveAvatarAsync(Guid userId, Guid? activeTenantId, CancellationToken cancel)
    {
        User? user = await users.FindByIdAsync(userId, cancel);
        if (user is null)
        {
            return IdentityErrors.SessionInvalid;
        }

        string? oldKey = user.AvatarKey;
        user.SetAvatarKey(null);
        await users.SaveChangesAsync(cancel);
        if (oldKey is not null)
        {
            await mediaStorage.DeleteAsync(oldKey, cancel);
        }

        return await GetProfileAsync(userId, activeTenantId, cancel);
    }

    public async Task<Result<byte[]>> GetAvatarByTokenAsync(Guid userId, string token, CancellationToken cancel)
    {
        if (!mediaSigner.TryValidateAvatarToken(token, timeProvider.GetUtcNow(), out Guid tokenUserId) || tokenUserId != userId)
        {
            return IdentityErrors.AvatarNotFound;
        }

        User? user = await users.FindByIdAsync(userId, cancel);
        byte[]? bytes = user?.AvatarKey is null ? null : await mediaStorage.ReadAsync(user.AvatarKey, cancel);
        return bytes is null ? IdentityErrors.AvatarNotFound : bytes;
    }

    private string? AvatarUrl(User user) => mediaSigner.CreateAvatarUrl(user, timeProvider.GetUtcNow());
}
