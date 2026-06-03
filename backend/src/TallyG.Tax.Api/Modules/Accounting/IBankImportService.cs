using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Accounting;

/// <summary>
/// Bank-statement import workflow: receive a PDF/Excel/CSV, store it, parse it into transaction
/// lines, run the matcher to suggest a counter-ledger per line, then (after review) post balanced
/// double-entry vouchers into the books — creating any adopted " (E)" ledgers on the way.
/// Auto-registered scoped by Scrutor (BankImportService : IBankImportService).
/// </summary>
public interface IBankImportService
{
    /// <summary>
    /// Receive and process a statement in one call: store bytes, parse, and match every line. The
    /// returned detail carries the lines with their suggestions for the review screen. If
    /// <paramref name="bankLedgerId"/> is null a default "Bank Account (E)" head is found/created.
    /// </summary>
    Task<BankImportDetailDto> UploadAsync(
        Stream body, string fileName, string? contentType, Guid? bankLedgerId, CancellationToken ct = default);

    Task<PagedResult<BankImportDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);

    Task<BankImportDetailDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Commit reviewed lines to the books: create adopted ledgers and post their vouchers.</summary>
    Task<PostImportResponse> PostAsync(Guid id, PostImportRequest request, CancellationToken ct = default);

    /// <summary>Soft-delete an import (blocked once any of its lines have been posted).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Push posted OtherIncome / SalesIncome credit lines from this bank import onto a tax return as
    /// nature-tagged IncomeSource records (savings/FD/other interest, dividend, business receipts).
    /// Idempotent per (import, return, ledger): re-running updates amounts rather than duplicating.
    /// Returns how many income-source rows were created or updated.
    /// </summary>
    Task<int> PushToReturnAsync(Guid importId, Guid returnId, CancellationToken ct = default);
}
