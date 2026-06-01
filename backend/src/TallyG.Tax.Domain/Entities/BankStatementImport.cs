using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// One uploaded bank statement (PDF / Excel / CSV) and the batch of transaction lines parsed from
/// it. The raw bytes live in object storage (<see cref="StoragePath"/>); the parsed rows are
/// <see cref="BankStatementLine"/> children. Posts against the bank account named by
/// <see cref="BankLedgerId"/>.
/// </summary>
public class BankStatementImport : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The bank-account ledger these lines are reconciled against.</summary>
    public Guid BankLedgerId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Object-storage key for the original upload.</summary>
    public string StoragePath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>SHA-256 of the uploaded bytes (dedupe + integrity).</summary>
    public string? Sha256 { get; set; }

    public BankImportStatus Status { get; set; } = BankImportStatus.Uploaded;

    public DateOnly? PeriodFrom { get; set; }
    public DateOnly? PeriodTo { get; set; }

    public int LineCount { get; set; }

    /// <summary>Lines the matcher tied to an existing ledger.</summary>
    public int MatchedCount { get; set; }

    /// <summary>Distinct " (E)" ledgers the matcher proposed for this statement.</summary>
    public int GeneratedLedgerCount { get; set; }

    public int PostedCount { get; set; }

    /// <summary>Free-form parser notes (skipped rows, ambiguous columns) as a JSON string array.</summary>
    public string? ParseWarningsJson { get; set; }

    public DateTimeOffset? PostedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<BankStatementLine> Lines { get; set; } = new List<BankStatementLine>();
}
