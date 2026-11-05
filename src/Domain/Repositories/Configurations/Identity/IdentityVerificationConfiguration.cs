using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Domain.Repositories.Configurations.Identity;

internal sealed class IdentityVerificationConfiguration : IEntityTypeConfiguration<IdentityVerification>
{
    public void Configure(EntityTypeBuilder<IdentityVerification> builder)
    {
        builder.ToTable("identity_verifications", DbSchemas.Identity);
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.CitizenId).HasMaxLength(12);
        builder.Property(v => v.FullName).HasMaxLength(100);
        builder.Property(v => v.Gender).HasMaxLength(8);
        builder.Property(v => v.Address).HasMaxLength(300);
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(v => v.SelfieKey).HasMaxLength(200);
        builder.Property(v => v.FrontKey).HasMaxLength(200);
        builder.Property(v => v.BackKey).HasMaxLength(200);
        builder.Property(v => v.RevokeReason).HasMaxLength(300);

        // Database tự chặn hai lần xác minh còn hiệu lực cho cùng tài khoản hoặc cùng số CCCD, kể cả khi hai request chạy song song.
        builder.HasIndex(v => v.UserId).IsUnique().HasFilter("status = 'Verified'");
        builder.HasIndex(v => v.CitizenId).IsUnique().HasFilter("status = 'Verified'");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
