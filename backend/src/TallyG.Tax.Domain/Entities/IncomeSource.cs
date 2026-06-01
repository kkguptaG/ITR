using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>Polymorphic header for each income head on a return (Ch.2 §2.5).</summary>
public class IncomeSource : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Guid TaxReturnId { get; set; }

    public IncomeType Type { get; set; }

    /// <summary>e.g. "Infosys Ltd", "Flat-2 Pune".</summary>
    public string? Label { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Head-specific extra metadata (jsonb on Postgres, text on Sqlite).</summary>
    public string SourceMetaJson { get; set; } = "{}";

    public DateTimeOffset? DeletedAt { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
