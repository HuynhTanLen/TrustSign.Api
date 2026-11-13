using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Domain.Repositories.Configurations.Identity;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", DbSchemas.Identity);
        builder.HasKey(tenant => tenant.Id);
        builder.Property(tenant => tenant.Id).ValueGeneratedNever();

        builder.Property(tenant => tenant.Name).HasMaxLength(200);
        builder.Property(tenant => tenant.TaxCode).HasMaxLength(32);
        builder.Property(tenant => tenant.Address).HasMaxLength(300);
        builder.Property(tenant => tenant.ContactEmail).HasMaxLength(254);
        builder.Property(tenant => tenant.RepresentativeName).HasMaxLength(100);
        builder.Property(tenant => tenant.RepresentativeTitle).HasMaxLength(100);
        builder.Property(tenant => tenant.Phone).HasMaxLength(32);
        builder.Property(tenant => tenant.LockReason).HasMaxLength(500);
        builder.Property(tenant => tenant.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(tenant => tenant.VerificationStatus).HasConversion<string>().HasMaxLength(16);
        builder.Property(tenant => tenant.VerificationRejectReason).HasMaxLength(500);
        builder.Property(tenant => tenant.BusinessLicenseKey).HasMaxLength(300);

        // Mỗi mã số thuế chỉ có một doanh nghiệp đang chờ duyệt hoặc đã xác thực; doanh nghiệp chưa nộp hồ sơ không bị ràng buộc.
        builder.HasIndex(tenant => tenant.TaxCode)
            .IsUnique()
            .HasFilter("tax_code IS NOT NULL AND verification_status IN ('Pending', 'Verified')");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(tenant => tenant.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
