using System.Globalization;
using System.Text;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Returns;

// ---------------------------------------------------------------------------
// Bulk capital-gain import (docs/architecture/11, Layer 2): a data-driven profile
// registry + a pure CSV parser that normalises a broker / MF statement into rows the
// importer can preview (with per-row errors + duplicate flags) and commit. New broker
// = a new ImportProfile (data), not a parser rewrite.
// ---------------------------------------------------------------------------

/// <summary>Request to preview (Commit=false) or commit (Commit=true) a CSV paste.</summary>
public sealed record CapitalGainImportRequest(string ProfileId, string Csv, bool Commit = false);

/// <summary>One parsed import row with its resolved fields, duplicate flag and any per-row errors.</summary>
public sealed record ImportedCgRow(
    int Row,
    CapitalGainAssetType AssetType,
    CapitalGainTerm Term,
    DateOnly? AcquisitionDate,
    DateOnly? TransferDate,
    decimal SalePrice,
    decimal CostOfAcquisition,
    decimal ExpensesOnTransfer,
    string? Isin,
    bool Duplicate,
    IReadOnlyList<string> Errors)
{
    public bool Ok => Errors.Count == 0 && !Duplicate;
}

public sealed record CapitalGainImportResult(
    string ProfileId,
    int TotalRows,
    int ValidRows,
    int DuplicateRows,
    int ErrorRows,
    int ImportedRows,
    IReadOnlyList<ImportedCgRow> Rows);

public sealed record CapitalGainImportProfileDto(string Id, string DisplayName);

/// <summary>Request to parse capital gains out of an already-extracted document (P5, AI-assisted parsing).</summary>
public sealed record ParseCapitalGainDocumentRequest(Guid DocumentId, bool Commit = false);

/// <summary>
/// Maps a capital-gains-statement extraction (the canonical <c>capgain.*</c> fields produced by the
/// document AI pipeline, each with a confidence) into import draft rows — reusing the same review/commit
/// model as the CSV importer. Low-confidence figures are flagged so they can't auto-commit without a human
/// look (docs/architecture/11, Layer 2C). Pure + deterministic.
/// </summary>
public static class CapitalGainDocumentParser
{
    public static IReadOnlyList<ImportedCgRow> ToRows(
        IReadOnlyDictionary<string, (decimal Value, decimal Confidence)> fields,
        decimal reviewThreshold = 0.92m)
    {
        var rows = new List<ImportedCgRow>();

        void Add(string key, CapitalGainTerm term)
        {
            if (!fields.TryGetValue(key, out var f) || f.Value <= 0m)
            {
                return;
            }

            var errors = f.Confidence < reviewThreshold
                ? new[] { $"Low extraction confidence ({f.Confidence:P0}) — verify before importing." }
                : (IReadOnlyList<string>)Array.Empty<string>();

            // Aggregate STT-paid equity gain figures: represent the gain as a sale with nil cost so the engine
            // routes them to 111A (short) / 112A (long).
            rows.Add(new ImportedCgRow(
                Row: rows.Count + 1,
                AssetType: CapitalGainAssetType.ListedEquity,
                Term: term,
                AcquisitionDate: null,
                TransferDate: null,
                SalePrice: f.Value,
                CostOfAcquisition: 0m,
                ExpensesOnTransfer: 0m,
                Isin: null,
                Duplicate: false,
                Errors: errors));
        }

        Add("capgain.equity_stcg_111a", CapitalGainTerm.Short);
        Add("capgain.equity_ltcg_112a", CapitalGainTerm.Long);
        return rows;
    }
}

/// <summary>A broker / statement format: which header names map to each logical column + the default asset class.</summary>
public sealed record ImportProfile(
    string Id,
    string DisplayName,
    CapitalGainAssetType DefaultAssetType,
    IReadOnlyDictionary<string, string[]> ColumnSynonyms);

public static class CapitalGainImportProfiles
{
    // Synonyms are matched case-insensitively against the de-spaced header, longest first.
    public static readonly IReadOnlyList<ImportProfile> All = new[]
    {
        new ImportProfile("generic", "Generic CSV", CapitalGainAssetType.ListedEquity, new Dictionary<string, string[]>
        {
            ["isin"] = new[] { "isin", "scrip", "symbol", "security", "name", "fundname" },
            ["buyDate"] = new[] { "buydate", "purchasedate", "acquisitiondate", "dateofpurchase", "datofacquisition" },
            ["sellDate"] = new[] { "selldate", "saledate", "transferdate", "dateofsale", "redemptiondate" },
            ["cost"] = new[] { "buyvalue", "purchasevalue", "cost", "costofacquisition", "acquisitioncost", "buyamount", "purchaseamount" },
            ["sale"] = new[] { "sellvalue", "salevalue", "saleconsideration", "sellamount", "saleamount", "redemptionamount", "consideration" },
            ["expenses"] = new[] { "expenses", "charges", "brokerage", "transfercost", "expensesontransfer" },
        }),
        new ImportProfile("zerodha", "Zerodha (equity P&L)", CapitalGainAssetType.ListedEquity, new Dictionary<string, string[]>
        {
            ["isin"] = new[] { "isin", "symbol", "scrip" },
            ["buyDate"] = new[] { "buydate", "entrydate", "purchasedate" },
            ["sellDate"] = new[] { "selldate", "exitdate", "saledate" },
            ["cost"] = new[] { "buyvalue", "buyamount", "purchasevalue" },
            ["sale"] = new[] { "sellvalue", "sellamount", "salevalue" },
            ["expenses"] = new[] { "charges", "brokerage", "expenses" },
        }),
        new ImportProfile("cams", "CAMS / KFintech (mutual funds)", CapitalGainAssetType.EquityMutualFund, new Dictionary<string, string[]>
        {
            ["isin"] = new[] { "isin", "schemename", "scheme", "fundname" },
            ["buyDate"] = new[] { "purchasedate", "acquisitiondate", "investmentdate", "buydate" },
            ["sellDate"] = new[] { "redemptiondate", "saledate", "selldate", "transferdate" },
            ["cost"] = new[] { "purchasecost", "costofacquisition", "acquisitioncost", "purchaseamount", "buyvalue" },
            ["sale"] = new[] { "redemptionamount", "saleamount", "saleconsideration", "sellvalue", "amount" },
            ["expenses"] = new[] { "stt", "charges", "expenses" },
        }),
    };

    public static ImportProfile? Find(string id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Pure CSV → import-row parser. No I/O; deterministic; unit-tested.</summary>
public static class CapitalGainCsvParser
{
    private static readonly string[] DateFormats =
        { "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MMM-yyyy", "dd MMM yyyy", "MM/dd/yyyy" };

    public static IReadOnlyList<ImportedCgRow> Parse(string csv, ImportProfile profile)
    {
        var lines = (csv ?? string.Empty)
            .Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Where(l => l.Trim().Length > 0)
            .ToList();
        if (lines.Count < 2)
        {
            return Array.Empty<ImportedCgRow>();
        }

        var header = SplitCsvLine(lines[0]).Select(Normalize).ToList();
        int Col(string logical)
        {
            if (!profile.ColumnSynonyms.TryGetValue(logical, out var syns))
            {
                return -1;
            }

            foreach (var syn in syns.OrderByDescending(s => s.Length))
            {
                var idx = header.FindIndex(h => h == syn || h.Contains(syn));
                if (idx >= 0)
                {
                    return idx;
                }
            }

            return -1;
        }

        int iIsin = Col("isin"), iBuy = Col("buyDate"), iSell = Col("sellDate"), iCost = Col("cost"), iSale = Col("sale"), iExp = Col("expenses");

        var rows = new List<ImportedCgRow>();
        for (var r = 1; r < lines.Count; r++)
        {
            var f = SplitCsvLine(lines[r]);
            string? Get(int i) => i >= 0 && i < f.Count ? f[i].Trim() : null;
            var errors = new List<string>();

            var sale = ParseMoney(Get(iSale));
            var cost = ParseMoney(Get(iCost));
            var exp = ParseMoney(Get(iExp)) ?? 0m;
            var buy = ParseDate(Get(iBuy));
            var sell = ParseDate(Get(iSell));

            if (iSale < 0) errors.Add("No sale-value column found.");
            else if (sale is null) errors.Add($"Row {r + 1}: sale value is not a number.");
            if (iCost >= 0 && cost is null) errors.Add($"Row {r + 1}: cost value is not a number.");

            var term = DeriveTerm(profile.DefaultAssetType, buy, sell);
            rows.Add(new ImportedCgRow(
                Row: r + 1,
                AssetType: profile.DefaultAssetType,
                Term: term,
                AcquisitionDate: buy,
                TransferDate: sell,
                SalePrice: sale ?? 0m,
                CostOfAcquisition: cost ?? 0m,
                ExpensesOnTransfer: exp,
                Isin: Get(iIsin),
                Duplicate: false,
                Errors: errors));
        }

        return rows;
    }

    /// <summary>A coarse holding-term guess for display (listed equity/MF 12m, else 24m). The engine derives
    /// the authoritative term from the rule-set at compute time.</summary>
    public static CapitalGainTerm DeriveTerm(CapitalGainAssetType asset, DateOnly? buy, DateOnly? sell)
    {
        if (buy is not { } b || sell is not { } s || s < b)
        {
            return CapitalGainTerm.Short;
        }

        var months = asset is CapitalGainAssetType.ListedEquity or CapitalGainAssetType.EquityMutualFund ? 12 : 24;
        return s > b.AddMonths(months) ? CapitalGainTerm.Long : CapitalGainTerm.Short;
    }

    /// <summary>Stable de-dup key: asset + both dates + sale + cost (rounded to the rupee).</summary>
    public static string DedupeKey(CapitalGainAssetType asset, DateOnly? buy, DateOnly? sell, decimal sale, decimal cost)
        => string.Join('|', (int)asset, buy?.ToString("yyyyMMdd") ?? "-", sell?.ToString("yyyyMMdd") ?? "-",
            Math.Round(sale), Math.Round(cost));

    private static string Normalize(string s) => new string((s ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static decimal? ParseMoney(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        var cleaned = new string(s.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static DateOnly? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        return DateOnly.TryParseExact(s.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            || DateOnly.TryParse(s.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
            ? d
            : null;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        fields.Add(sb.ToString());
        return fields;
    }
}
