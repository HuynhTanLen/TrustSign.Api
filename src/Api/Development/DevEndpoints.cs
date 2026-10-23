using TrustSign.Application.Modules.Identity;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Api.Development;

/// <summary>
/// Chỉ được map khi chạy ở môi trường Development. Cho phép lấy token để thử các API cần đăng nhập
/// trước khi có đăng nhập thật. Xóa thư mục này khi UC04 (đăng nhập) đã xong.
/// </summary>
internal static class DevEndpoints
{
    private const string DevEmail = "dev@trustsign.local";

    public static void MapDevEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/dev-token", async (
            IUserRepository users, IAccessTokenIssuer issuer, TimeProvider time, CancellationToken cancel) =>
        {
            User? user = await users.FindByEmailAsync(DevEmail, cancel);
            if (user is null)
            {
                // "!" không phải mã băm hợp lệ nên tài khoản này không đăng nhập bằng mật khẩu được.
                user = User.Register(DevEmail, "Tài khoản thử", passwordHash: "!", time.GetUtcNow());
                user.MarkEmailVerified();
                await users.AddAsync(user, cancel);
            }

            AccessTokenDto token = issuer.Issue(user.Id, sessionId: Guid.CreateVersion7(), user.Role);
            return Results.Ok(token);
        });
    }
}
