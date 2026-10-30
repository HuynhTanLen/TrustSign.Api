using Microsoft.EntityFrameworkCore;
using TrustSign.Domain.Entities.Contracts;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.Entities.Subscriptions;

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

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    public DbSet<ContractTemplate> ContractTemplates => Set<ContractTemplate>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractSigner> ContractSigners => Set<ContractSigner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrustSignDbContext).Assembly);
}
