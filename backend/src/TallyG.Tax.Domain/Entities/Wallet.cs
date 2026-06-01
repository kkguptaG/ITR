using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>One wallet per user; non-negative balance (Ch.2 §2.7).</summary>
public class Wallet : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public decimal Balance { get; set; }
    public string Currency { get; set; } = "INR";

    public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}
