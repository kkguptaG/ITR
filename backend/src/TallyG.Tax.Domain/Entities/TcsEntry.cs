using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// One collector-wise TCS row (tax collected at source) from the assessee's 26AS / Form 27D — e.g. TCS
/// on a foreign remittance under LRS, a motor-vehicle purchase, or by a seller. The building block of the
/// ITR's Schedule TCS. Captured in the assessee's own hands and claimed this year. Return-scoped.
/// </summary>
public class TcsEntry : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>Collector's TAN (e.g. DELH12345A).</summary>
    public string CollectorTan { get; set; } = string.Empty;

    public string CollectorName { get; set; } = string.Empty;

    /// <summary>Tax collected at source (and claimed this year, in the assessee's own hands).</summary>
    public decimal TcsCollected { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
