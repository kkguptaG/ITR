using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// One itemised 80G donation (a single donee), disclosed donee-wise in Schedule 80G. Since AY2018-19 the
/// ITD requires the donee's name + PAN for every 80G claim, so the headline "totals only" shape is not
/// accepted for a real return — each donation must be reported as a row in one of the four rate buckets.
/// Return-scoped (a donation belongs to the year it was made).
/// </summary>
public class Donation80G : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>Name of the donee institution (≤125 chars).</summary>
    public string DoneeName { get; set; } = string.Empty;

    /// <summary>Donee PAN (mandatory for the 80G claim; ITD PAN format ABCDE1234F).</summary>
    public string DoneePan { get; set; } = string.Empty;

    /// <summary>Donation reference / ARN number from the donee's 80G certificate (optional, ≤25 chars).</summary>
    public string? ArnNumber { get; set; }

    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    /// <summary>ITD two-digit state code ("01".."38").</summary>
    public string StateCode { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;

    /// <summary>The rate bucket (100%/50%, with/without qualifying limit) the donation falls in.</summary>
    public Donation80GCategory Category { get; set; }

    /// <summary>Donated in cash (a cash donation over ₹2,000 is wholly disallowed for 80G).</summary>
    public decimal CashAmount { get; set; }

    /// <summary>Donated by any non-cash mode (cheque / UPI / bank transfer).</summary>
    public decimal OtherModeAmount { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
