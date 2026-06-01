using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>A purchasable plan / pricing tier (Ch.2 §2.7).</summary>
public class Plan : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    /// <summary>one_time | annual.</summary>
    public string BillingPeriod { get; set; } = "one_time";

    /// <summary>Feature list (jsonb on Postgres, text on Sqlite).</summary>
    public string Features { get; set; } = "[]";

    public bool IsActive { get; set; } = true;
}
