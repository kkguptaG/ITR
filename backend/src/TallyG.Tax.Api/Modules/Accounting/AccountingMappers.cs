using System.Text.Json;
using TallyG.Tax.Domain.Accounting;
using TallyG.Tax.Domain.Entities;

namespace TallyG.Tax.Api.Modules.Accounting;

/// <summary>Entity → DTO projections shared by the ledger and bank-import services.</summary>
internal static class AccountingMappers
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Project a ledger to its DTO. <paramref name="postedDebits"/>/<paramref name="postedCredits"/>
    /// are the sums of this ledger's voucher entries; the current balance is opening ± net movement,
    /// signed by the account's normal side.
    /// </summary>
    public static LedgerDto ToDto(Ledger l, decimal postedDebits, decimal postedCredits, int voucherCount)
    {
        var net = LedgerGroupMeta.IsDebitNormal(l.Nature)
            ? postedDebits - postedCredits
            : postedCredits - postedDebits;

        return new LedgerDto(
            l.Id,
            l.Name,
            l.Group,
            l.Nature,
            l.OpeningBalance,
            decimal.Round(l.OpeningBalance + net, 2),
            l.IsBank,
            l.IsSystemGenerated,
            l.Notes,
            voucherCount,
            l.CreatedAt,
            l.UpdatedAt);
    }

    public static BankImportDto ToDto(BankStatementImport import, string bankLedgerName) => new(
        import.Id,
        import.BankLedgerId,
        bankLedgerName,
        import.FileName,
        import.ContentType,
        import.SizeBytes,
        import.Status,
        import.PeriodFrom,
        import.PeriodTo,
        import.LineCount,
        import.MatchedCount,
        import.GeneratedLedgerCount,
        import.PostedCount,
        DeserializeWarnings(import.ParseWarningsJson),
        import.PostedAt,
        import.CreatedAt);

    public static BankLineDto ToDto(BankStatementLine line) => new(
        line.Id,
        line.RowIndex,
        line.TxnDate,
        line.Narration,
        line.ReferenceNo,
        line.Debit,
        line.Credit,
        line.RunningBalance,
        line.Direction,
        line.Amount,
        line.SuggestedLedgerId,
        line.SuggestedLedgerName,
        line.SuggestedGroup,
        line.SuggestionIsNewLedger,
        line.MatchConfidence,
        line.MatchMethod,
        line.MatchRationale,
        line.ChosenLedgerId,
        line.Status,
        line.VoucherId);

    public static string SerializeWarnings(IReadOnlyList<string> warnings)
        => JsonSerializer.Serialize(warnings, JsonOptions);

    public static IReadOnlyList<string> DeserializeWarnings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
