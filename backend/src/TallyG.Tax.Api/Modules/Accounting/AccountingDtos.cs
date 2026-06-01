// Accounting module — request/response DTOs (the public contract consumed by the frontend).
// Enums (LedgerGroup, LedgerNature, DrCr, VoucherType, statuses) serialize as their string names
// via the global JsonStringEnumConverter, matching the frontend's string-union types.

using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Accounting;

// ============================================================ Chart of accounts

/// <summary>A chart-of-accounts head with its live balance.</summary>
public sealed record LedgerDto(
    Guid Id,
    string Name,
    LedgerGroup Group,
    LedgerNature Nature,
    decimal OpeningBalance,
    decimal CurrentBalance,
    bool IsBank,
    bool IsSystemGenerated,
    string? Notes,
    int VoucherCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Create a ledger. <paramref name="Group"/> fixes the account's nature and normal side.</summary>
public sealed record CreateLedgerRequest(
    string Name,
    LedgerGroup Group,
    decimal OpeningBalance = 0m,
    bool IsBank = false,
    string? Notes = null);

/// <summary>
/// Update a ledger. Saving "adopts" a system-generated head: <c>IsSystemGenerated</c> is cleared so
/// the " (E)" trace no longer applies (the caller should also drop the suffix from the name).
/// </summary>
public sealed record UpdateLedgerRequest(
    string Name,
    LedgerGroup Group,
    decimal OpeningBalance = 0m,
    string? Notes = null);

// ============================================================ Bank statement import

/// <summary>Summary of an uploaded bank statement and its matching/posting progress.</summary>
public sealed record BankImportDto(
    Guid Id,
    Guid BankLedgerId,
    string BankLedgerName,
    string FileName,
    string ContentType,
    long SizeBytes,
    BankImportStatus Status,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    int LineCount,
    int MatchedCount,
    int GeneratedLedgerCount,
    int PostedCount,
    IReadOnlyList<string> Warnings,
    DateTimeOffset? PostedAt,
    DateTimeOffset CreatedAt);

/// <summary>One parsed statement line with the matcher's suggestion and review/posting state.</summary>
public sealed record BankLineDto(
    Guid Id,
    int RowIndex,
    DateOnly? TxnDate,
    string Narration,
    string? ReferenceNo,
    decimal? Debit,
    decimal? Credit,
    decimal? RunningBalance,
    DrCr Direction,
    decimal Amount,
    Guid? SuggestedLedgerId,
    string? SuggestedLedgerName,
    LedgerGroup? SuggestedGroup,
    bool SuggestionIsNewLedger,
    decimal MatchConfidence,
    string? MatchMethod,
    string? MatchRationale,
    Guid? ChosenLedgerId,
    BankLineStatus Status,
    Guid? VoucherId);

/// <summary>An import together with its parsed lines (the review payload).</summary>
public sealed record BankImportDetailDto(
    BankImportDto Import,
    IReadOnlyList<BankLineDto> Lines);

// --- commit (post vouchers) ---

/// <summary>Spec for a ledger to create on the fly while posting (an adopted/edited " (E)" proposal).</summary>
public sealed record NewLedgerSpec(string Name, LedgerGroup Group);

/// <summary>
/// A per-line posting decision. Omit a line to accept its suggestion as-is. Set <paramref name="Skip"/>
/// to exclude it. Otherwise supply <paramref name="LedgerId"/> (an existing head) or
/// <paramref name="NewLedger"/> (create a head) to override the suggested counter-ledger.
/// </summary>
public sealed record LineDecision(
    Guid LineId,
    bool Skip = false,
    Guid? LedgerId = null,
    NewLedgerSpec? NewLedger = null);

/// <summary>
/// Commit an import to the books. Lines listed in <paramref name="Decisions"/> follow those choices;
/// lines not listed accept their suggestion when <paramref name="PostUnlistedSuggestions"/> is true
/// (the "approve all" path), or are left untouched when false.
/// </summary>
public sealed record PostImportRequest(
    IReadOnlyList<LineDecision>? Decisions = null,
    bool PostUnlistedSuggestions = true);

/// <summary>Outcome of committing an import.</summary>
public sealed record PostImportResponse(
    BankImportDto Import,
    int VouchersPosted,
    int LedgersCreated,
    int Skipped,
    IReadOnlyList<LedgerDto> CreatedLedgers);
