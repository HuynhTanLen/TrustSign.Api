using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSign.Domain.Entities.Contracts;

namespace TrustSign.Domain.Repositories.Configurations.Contracts;

internal sealed class ContractTemplateConfiguration : IEntityTypeConfiguration<ContractTemplate>
{
    public void Configure(EntityTypeBuilder<ContractTemplate> builder)
    {
        builder.ToTable("templates", DbSchemas.Contract);
        builder.HasKey(tpl => tpl.Id);
        builder.Property(tpl => tpl.Id).ValueGeneratedNever();

        builder.HasIndex(tpl => new { tpl.TenantId, tpl.UpdatedAt });
        builder.Property(tpl => tpl.Name).HasMaxLength(200);
        builder.Property(tpl => tpl.AuthorName).HasMaxLength(100);
        builder.Property(tpl => tpl.VariablesJson).HasMaxLength(4000);
    }
}

internal sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("contracts", DbSchemas.Contract);
        builder.HasKey(contract => contract.Id);
        builder.Property(contract => contract.Id).ValueGeneratedNever();

        builder.Property(contract => contract.Code).HasMaxLength(32);
        builder.HasIndex(contract => contract.Code).IsUnique();
        builder.HasIndex(contract => new { contract.TenantId, contract.CreatedAt });
        builder.HasIndex(contract => new { contract.TenantId, contract.Status });

        builder.Property(contract => contract.Title).HasMaxLength(200);
        builder.Property(contract => contract.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(contract => contract.StorageKey).HasMaxLength(500);
        builder.Property(contract => contract.OriginalFileHash).HasMaxLength(64);
        builder.Property(contract => contract.CurrentFileHash).HasMaxLength(64);
        builder.Property(contract => contract.CreatorName).HasMaxLength(100);

        builder.HasMany(contract => contract.Signers)
            .WithOne()
            .HasForeignKey(signer => signer.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(contract => contract.Signers)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ContractSignerConfiguration : IEntityTypeConfiguration<ContractSigner>
{
    public void Configure(EntityTypeBuilder<ContractSigner> builder)
    {
        builder.ToTable("contract_signers", DbSchemas.Contract);
        builder.HasKey(signer => signer.Id);
        builder.Property(signer => signer.Id).ValueGeneratedNever();

        builder.HasIndex(signer => new { signer.ContractId, signer.SignOrder }).IsUnique();
        builder.Property(signer => signer.FullName).HasMaxLength(100);
        builder.Property(signer => signer.Email).HasMaxLength(254);
        builder.Property(signer => signer.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(signer => signer.DeclineReason).HasMaxLength(500);
    }
}
