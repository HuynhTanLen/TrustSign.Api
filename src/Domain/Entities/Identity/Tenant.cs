using TrustSign.Common.Enums;

namespace TrustSign.Domain.Entities.Identity;

public sealed class Tenant
{
    private Tenant()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string? TaxCode { get; private set; }

    public string? Address { get; private set; }

    public string? ContactEmail { get; private set; }

    public TenantStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Tenant Create(
        string name,
        string? taxCode,
        string? address,
        string? contactEmail,
        Guid createdByUserId,
        DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        Name = name.Trim(),
        TaxCode = string.IsNullOrWhiteSpace(taxCode) ? null : taxCode.Trim(),
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : User.NormalizeEmail(contactEmail),
        Status = TenantStatus.Active,
        CreatedByUserId = createdByUserId,
        CreatedAt = now
    };
}
