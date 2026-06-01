using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>One claimed deduction line, keyed by section code (Ch.2 §2.5).</summary>
public class Deduction : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>e.g. "80C", "80D", "80CCD(1B)", "80TTA", "24b".</summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>e.g. "lic", "elss", "self_health".</summary>
    public string? SubType { get; set; }

    public string? Description { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Amount allowed after Ch.3 caps are applied.</summary>
    public decimal? EligibleAmount { get; set; }

    /// <summary>Regime under which this deduction applies (many vanish under New).</summary>
    public Regime? RegimeApplicable { get; set; }

    public Guid? ProofDocumentId { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
