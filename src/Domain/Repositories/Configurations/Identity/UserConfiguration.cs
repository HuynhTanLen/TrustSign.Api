using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Domain.Repositories.Configurations.Identity;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", DbSchemas.Identity);
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.Email).HasMaxLength(254);
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.FullName).HasMaxLength(100);
        builder.Property(user => user.PasswordHash).HasMaxLength(512);
        builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(16);
        builder.Property(user => user.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(user => user.LockReason).HasMaxLength(500);

        builder.Property(user => user.PhoneNumber).HasMaxLength(32);
        builder.Property(user => user.JobTitle).HasMaxLength(100);
        builder.Property(user => user.AvatarKey).HasMaxLength(200);
        builder.Property(user => user.TermsVersion).HasMaxLength(32);
    }
}
