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
        builder.Property(tenant => tenant.Status).HasConversion<string>().HasMaxLength(16);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(tenant => tenant.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
