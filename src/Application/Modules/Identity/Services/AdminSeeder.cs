using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Identity;

/// <summary>
/// Tạo tài khoản admin từ mục <c>AdminSeed</c> nếu email đó chưa có. Chạy lại nhiều lần không sao:
/// tài khoản đã tồn tại thì giữ nguyên, kể cả mật khẩu và vai trò.
/// </summary>
internal sealed class AdminSeeder(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IOptions<AdminSeedOptions> options,
    TimeProvider timeProvider,
    ILogger<AdminSeeder> logger) : IAdminSeeder
{
    private readonly AdminSeedOptions _options = options.Value;

    public async Task SeedAsync(CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrEmpty(_options.Password))
        {
            logger.LogInformation("Bỏ qua tạo tài khoản admin vì chưa cấu hình AdminSeed:Email và AdminSeed:Password");
            return;
        }

        User? existing = await users.FindByEmailAsync(_options.Email, cancel);
        if (existing is not null)
        {
            logger.LogInformation("Tài khoản {Email} đã tồn tại với vai trò {Role}, không tạo lại", existing.Email, existing.Role);
            return;
        }

        User admin = User.CreateAdmin(_options.Email, _options.FullName, passwordHasher.Hash(_options.Password), timeProvider.GetUtcNow());
        await users.AddAsync(admin, cancel);
        logger.LogInformation("Đã tạo tài khoản admin {Email}", admin.Email);
    }
}
