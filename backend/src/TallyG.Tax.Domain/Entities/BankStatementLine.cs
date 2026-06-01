using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A single parsed transaction row from a <see cref="BankStatementImport"/>, together with the
/// matcher's suggested counter-ledger and the user's review outcome. Exactly one of
/// <see cref="Debit"/> / <see cref="Credit"/> carries the amount; <see cref="Direction"/> and
/// <see cref="Amount"/> are the normalised view used when posting the voucher.
/// </summary>
public class BankStatementLine : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ImportId { get; set; }

    /// <summary>1-based position within the statement (parse order).</summary>
    public int RowIndex { get; set; }

    public DateOnly? TxnDate { get; set; }

    public string Narration { get; set; } = string.Empty;

    public string? ReferenceNo { get; set; }

    /// <summary>Money out of the bank (a withdrawal/payment), if any.</summary>
    public decimal? Debit { get; set; }

    /// <summary>Money into the bank (a deposit/receipt), if any.</summary>
    public decimal? Credit { get; set; }

    /// <summary>Statement running balance after this row, when the statement reports one.</summary>
    public decimal? RunningBalance { get; set; }

    /// <summary>Normalised direction of the bank movement: Debit = money out, Credit = money in.</summary>
    public DrCr Direction { get; set; }

    /// <summary>Absolute transaction amount (the populated one of Debit/Credit).</summary>
    public decimal Amount { get; set; }

    // ---- matcher result ----

    /// <summary>The existing ledger the matcher chose, when it matched one.</summary>
    public Guid? SuggestedLedgerId { get; set; }

    /// <summary>Name of the suggested counter-ledger (an existing head, or a proposed " (E)" head).</summary>
    public string? SuggestedLedgerName { get; set; }

    /// <summary>Group the suggested/proposed counter-ledger belongs to.</summary>
    public LedgerGroup? SuggestedGroup { get; set; }

    /// <summary>True when the suggestion is to CREATE a new " (E)" ledger rather than reuse an existing one.</summary>
    public bool SuggestionIsNewLedger { get; set; }

    /// <summary>Matcher confidence in [0,1].</summary>
    public decimal MatchConfidence { get; set; }

    /// <summary>How the match was made, e.g. "existing-name", "keyword", "counterparty-new", "fallback".</summary>
    public string? MatchMethod { get; set; }

    /// <summary>Human-readable explanation of the suggestion (shown in the review drawer).</summary>
    public string? MatchRationale { get; set; }

    // ---- review / posting outcome ----

    /// <summary>The ledger the line was finally posted against (existing or freshly created).</summary>
    public Guid? ChosenLedgerId { get; set; }

    public BankLineStatus Status { get; set; } = BankLineStatus.Suggested;

    /// <summary>The voucher created when this line was posted.</summary>
    public Guid? VoucherId { get; set; }
}
