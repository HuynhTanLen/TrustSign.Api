using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSign.Domain.Entities.Audit;

namespace TrustSign.Domain.Repositories.Configurations.Audit;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events", DbSchemas.Audit);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Action).HasMaxLength(100);
        builder.Property(e => e.ResourceType).HasMaxLength(100);
        builder.Property(e => e.ResourceId).HasMaxLength(100);
        builder.Property(e => e.ActorEmail).HasMaxLength(254);
        builder.Property(e => e.PrevHash).HasMaxLength(64);
        builder.Property(e => e.Hash).HasMaxLength(64);

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.Hash);
    }
}
