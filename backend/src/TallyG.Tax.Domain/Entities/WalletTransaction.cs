using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>Append-only wallet ledger entry; balance_after snapshots make audits O(1).</summary>
public class WalletTransaction : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid WalletId { get; set; }

    public WalletTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }

    /// <summary>e.g. payment / referral / manual.</summary>
    public string? Reference { get; set; }
    public string? Note { get; set; }

    public Wallet? Wallet { get; set; }
}
