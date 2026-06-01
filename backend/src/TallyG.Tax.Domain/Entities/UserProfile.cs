using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// PII-heavy profile, split from <see cref="User"/> to keep the auth hot path lean and
/// the DPDP export/erasure boundary clean (Ch.2 §2.4).
/// </summary>
public class UserProfile : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? Dob { get; set; }
    public string? Gender { get; set; }
    public string? FatherName { get; set; }
    public string? AadhaarLast4 { get; set; }

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateCode { get; set; }
    public string? Pincode { get; set; }

    /// <summary>resident / rnor / non_resident.</summary>
    public string? ResidentialStatus { get; set; }

    /// <summary>salaried / freelancer / trader / professional / pensioner / msme.</summary>
    public string? OccupationType { get; set; }

    public string? BankAccountNoEnc { get; set; }
    public string? BankIfsc { get; set; }
    public bool IsGovtEmployee { get; set; }

    public User? User { get; set; }
}
