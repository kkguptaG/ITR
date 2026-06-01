using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A posted accounting voucher — the double-entry record created when a reviewed bank statement
/// line is committed to the books. Its <see cref="Entries"/> always balance (total debits == total
/// credits == <see cref="Amount"/>). A bank receipt posts Dr Bank / Cr counter-ledger; a bank
/// payment posts Dr counter-ledger / Cr Bank.
/// </summary>
public class Voucher : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Provenance: the statement import this voucher was generated from (null = manual).</summary>
    public Guid? ImportId { get; set; }

    /// <summary>The specific statement line this voucher posts (null = manual).</summary>
    public Guid? BankStatementLineId { get; set; }

    public VoucherType Type { get; set; }

    public DateOnly Date { get; set; }

    public string? Narration { get; set; }

    public string? ReferenceNo { get; set; }

    /// <summary>Total voucher value (one side of the balanced entry).</summary>
    public decimal Amount { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<VoucherEntry> Entries { get; set; } = new List<VoucherEntry>();
}
