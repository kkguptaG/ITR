namespace TallyG.Tax.Domain.Abstractions;

/// <summary>
/// Turns the raw bytes of an uploaded bank statement into a normalised list of transaction rows.
/// Excel (.xlsx) and CSV are parsed deterministically from their tabular structure; PDF is parsed
/// best-effort by reconstructing text rows from word positions. The implementation lives in
/// Infrastructure (it pulls in ClosedXML / PdfPig); the interface is here so the Accounting module
/// depends only on the Domain abstraction. Registered explicitly in <c>AddInfrastructure</c>.
/// </summary>
public interface IBankStatementParser
{
    /// <summary>
    /// Parse a statement. Implementations are pure (no persistence) and never throw on a malformed
    /// row — unusable rows are skipped and noted in <see cref="BankStatementParseResult.Warnings"/>.
    /// </summary>
    BankStatementParseResult Parse(byte[] content, string contentType, string fileName);
}

/// <summary>One normalised statement row. Exactly one of Debit/Credit is expected to be set.</summary>
public sealed record ParsedBankLine(
    int RowIndex,
    DateOnly? Date,
    string Narration,
    string? ReferenceNo,
    decimal? Debit,
    decimal? Credit,
    decimal? Balance);

/// <summary>The outcome of parsing a statement: the rows, any warnings, and the detected period.</summary>
public sealed record BankStatementParseResult(
    IReadOnlyList<ParsedBankLine> Lines,
    IReadOnlyList<string> Warnings,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo)
{
    public static BankStatementParseResult Empty(string warning)
        => new(Array.Empty<ParsedBankLine>(), new[] { warning }, null, null);
}
