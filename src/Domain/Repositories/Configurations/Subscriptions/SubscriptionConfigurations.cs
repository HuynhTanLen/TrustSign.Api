using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSign.Domain.Entities.Subscriptions;

namespace TrustSign.Domain.Repositories.Configurations.Subscriptions;

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans", DbSchemas.Subscription);
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).ValueGeneratedNever();

        builder.Property(plan => plan.Name).HasMaxLength(100);
        builder.Property(plan => plan.Price).HasPrecision(18, 2);
    }
}

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions", DbSchemas.Subscription);
        builder.HasKey(sub => sub.Id);
        builder.Property(sub => sub.Id).ValueGeneratedNever();

        builder.HasIndex(sub => sub.TenantId).IsUnique();
        builder.Property(sub => sub.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(sub => new { sub.Status, sub.ExpiresAt });

        builder.HasOne<Plan>()
            .WithMany()
            .HasForeignKey(sub => sub.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices", DbSchemas.Subscription);
        builder.HasKey(invoice => invoice.Id);
        builder.Property(invoice => invoice.Id).ValueGeneratedNever();

        builder.Property(invoice => invoice.Code).HasMaxLength(16);
        builder.HasIndex(invoice => invoice.Code).IsUnique();
        builder.HasIndex(invoice => new { invoice.TenantId, invoice.CreatedAt });

        builder.Property(invoice => invoice.PlanName).HasMaxLength(100);
        builder.Property(invoice => invoice.Amount).HasPrecision(18, 2);
        builder.Property(invoice => invoice.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(invoice => invoice.BankReference).HasMaxLength(100);
        builder.HasIndex(invoice => invoice.SepayTransactionId).IsUnique();

        builder.HasOne<Plan>()
            .WithMany()
            .HasForeignKey(invoice => invoice.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions", DbSchemas.Subscription);
        builder.HasKey(tx => tx.Id);
        builder.Property(tx => tx.Id).ValueGeneratedNever();

        builder.HasIndex(tx => tx.SepayTransactionId).IsUnique();
        builder.Property(tx => tx.Gateway).HasMaxLength(100);
        builder.Property(tx => tx.AccountNumber).HasMaxLength(50);
        builder.Property(tx => tx.Content).HasMaxLength(500);
        builder.Property(tx => tx.ReferenceCode).HasMaxLength(100);
        builder.Property(tx => tx.TransferAmount).HasPrecision(18, 2);
        builder.Property(tx => tx.TransferType).HasMaxLength(10);
        builder.Property(tx => tx.TransactionDate).HasMaxLength(40);
        builder.Property(tx => tx.InvoiceCode).HasMaxLength(16);
        builder.Property(tx => tx.Outcome).HasMaxLength(32);
    }
}
