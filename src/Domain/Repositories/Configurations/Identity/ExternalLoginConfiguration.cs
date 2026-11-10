using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Domain.Repositories.Configurations.Identity;

internal sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("external_logins", DbSchemas.Identity);
        builder.HasKey(login => login.Id);
        builder.Property(login => login.Id).ValueGeneratedNever();

        builder.Property(login => login.Provider).HasMaxLength(32);
        builder.Property(login => login.ProviderUserId).HasMaxLength(128);
        builder.Property(login => login.Email).HasMaxLength(254);

        builder.HasIndex(login => new { login.Provider, login.ProviderUserId }).IsUnique();
        // Mỗi tài khoản chỉ liên kết một danh tính cho mỗi nhà cung cấp.
        builder.HasIndex(login => new { login.UserId, login.Provider }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(login => login.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
