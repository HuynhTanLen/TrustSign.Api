using Microsoft.EntityFrameworkCore;
using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Domain.Repositories;

/// <summary>
/// DbContext duy nhất của hệ thống. Cấu hình bảng nằm trong <c>Configurations/&lt;Module&gt;/</c>,
/// mỗi bảng khai báo schema của module mình qua <see cref="DbSchemas"/>.
/// </summary>
public sealed class TrustSignDbContext(DbContextOptions<TrustSignDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrustSignDbContext).Assembly);
}
