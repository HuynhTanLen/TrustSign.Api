using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Common.Validation;

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

    public string? RepresentativeName { get; private set; }

    public string? RepresentativeTitle { get; private set; }

    public string? Phone { get; private set; }

    public TenantStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Tenant Create(
        string name,
        string? taxCode,
        string? address,
        string? contactEmail,
        Guid createdByUserId,
        DateTimeOffset now,
        string? representativeName = null,
        string? representativeTitle = null,
        string? phone = null) => new()
    {
        Id = Guid.CreateVersion7(now),
        Name = name.Trim(),
        TaxCode = string.IsNullOrWhiteSpace(taxCode) ? null : taxCode.Trim(),
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : User.NormalizeEmail(contactEmail),
        RepresentativeName = string.IsNullOrWhiteSpace(representativeName) ? null : representativeName.Trim(),
        RepresentativeTitle = string.IsNullOrWhiteSpace(representativeTitle) ? null : representativeTitle.Trim(),
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
        Status = TenantStatus.Active,
        CreatedByUserId = createdByUserId,
        CreatedAt = now
    };

    public Result Update(
        string name,
        string? taxCode,
        string? address,
        string? contactEmail,
        string? phone,
        string? representativeName,
        string? representativeTitle)
    {
        string trimmedName = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName) || trimmedName.Length > 200)
        {
            return IdentityErrors.InvalidTenantName;
        }

        if (!string.IsNullOrWhiteSpace(taxCode) && !TaxCodeValidator.IsValid(taxCode))
        {
            return IdentityErrors.InvalidTaxCode;
        }

        if (!string.IsNullOrWhiteSpace(contactEmail) && !EmailValidator.IsValid(contactEmail))
        {
            return IdentityErrors.InvalidEmail;
        }

        if (!string.IsNullOrWhiteSpace(phone) && !PhoneValidator.IsValid(phone))
        {
            return IdentityErrors.InvalidPhoneNumber;
        }

        if (!string.IsNullOrWhiteSpace(representativeName) && representativeName.Trim().Length > 100)
        {
            return IdentityErrors.InvalidRepresentativeName;
        }

        if (!string.IsNullOrWhiteSpace(representativeTitle) && representativeTitle.Trim().Length > 100)
        {
            return IdentityErrors.InvalidRepresentativeTitle;
        }

        Name = trimmedName;
        TaxCode = string.IsNullOrWhiteSpace(taxCode) ? null : taxCode.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : User.NormalizeEmail(contactEmail);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        RepresentativeName = string.IsNullOrWhiteSpace(representativeName) ? null : representativeName.Trim();
        RepresentativeTitle = string.IsNullOrWhiteSpace(representativeTitle) ? null : representativeTitle.Trim();
        return Result.Success();
    }
}
