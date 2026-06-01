using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A chart-of-accounts head (an "account ledger" in Tally terms) within a user's standalone books.
/// Bank statement lines are posted against these ledgers via double-entry vouchers.
///
/// When the matcher cannot map a line to an existing ledger it proposes a new one whose name carries
/// a trailing " (E)" mark and whose <see cref="IsSystemGenerated"/> flag is set — so the user can
/// trace exactly which heads the system created and rename/regroup them. Clearing the flag (e.g. by
/// editing the ledger) is how the user "adopts" a generated head.
/// </summary>
public class Ledger : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }

    /// <summary>Owner of the books these ledgers belong to (standalone, not tied to a return).</summary>
    public Guid UserId { get; set; }

    /// <summary>Display name, e.g. "Rent", "Salaries", "Electricity", or a generated "Swiggy (E)".</summary>
    public string Name { get; set; } = string.Empty;

    public LedgerGroup Group { get; set; }

    /// <summary>Derived from <see cref="Group"/> at creation; kept denormalised for querying/reporting.</summary>
    public LedgerNature Nature { get; set; }

    public decimal OpeningBalance { get; set; }

    /// <summary>True for ledgers under the Bank Accounts group (a statement can be imported against them).</summary>
    public bool IsBank { get; set; }

    /// <summary>
    /// True when this head was auto-created by the matcher (the " (E)" trace). Cleared when the user
    /// edits/adopts the ledger so it is no longer flagged as machine-generated.
    /// </summary>
    public bool IsSystemGenerated { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
