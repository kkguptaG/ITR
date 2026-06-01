using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Accounting;

/// <summary>
/// Maps a bank-statement line's narration to the best-fit counter-ledger. It first tries to reuse an
/// existing account head, then a built-in category keyword table, and finally proposes a brand-new
/// head named after the counterparty (which the caller marks with the " (E)" trace).
///
/// This is the deterministic, swappable analogue of the OCR <c>IExtractionService</c>: in production
/// it would call Claude with the user's chart of accounts as context; here it is a transparent rule
/// engine whose <see cref="LedgerSuggestion.Rationale"/> explains every decision. Auto-registered
/// scoped by Scrutor (LedgerMatchingService : ILedgerMatchingService).
/// </summary>
public interface ILedgerMatchingService
{
    /// <summary>
    /// Suggest a counter-ledger for one statement line. <paramref name="direction"/> is the bank
    /// movement (Debit = money out → an expense/payment; Credit = money in → an income/receipt),
    /// which disambiguates categories that differ by direction (e.g. interest paid vs earned).
    /// </summary>
    LedgerSuggestion Suggest(string narration, DrCr direction, IReadOnlyCollection<Ledger> existing);
}

/// <summary>
/// The matcher's verdict for a line. When <see cref="IsNew"/> is true the caller should create a
/// ledger named <see cref="LedgerName"/> (already carrying the " (E)" mark) under <see cref="Group"/>;
/// otherwise <see cref="ExistingLedgerId"/> identifies the head to reuse.
/// </summary>
public sealed record LedgerSuggestion(
    Guid? ExistingLedgerId,
    string LedgerName,
    LedgerGroup Group,
    bool IsNew,
    decimal Confidence,
    string Method,
    string Rationale);
