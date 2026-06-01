using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using TallyG.Tax.Domain.Abstractions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace TallyG.Tax.Infrastructure.Services;

/// <summary>
/// Deterministic bank-statement parser for the Accounting module. Excel (.xlsx) and CSV are read
/// from their tabular structure; PDF is reconstructed best-effort from word positions. All three
/// funnel into a single <see cref="ExtractFromGrid"/> that detects the header row, maps columns by
/// header synonyms (date / particulars / withdrawal / deposit / balance), and types each data row.
///
/// The parser never throws on bad input — unusable rows are skipped and noted as warnings — so the
/// caller can surface a partial result rather than failing the whole upload. Registered as a
/// singleton in <c>AddInfrastructure</c> (it holds no state).
/// </summary>
public sealed class BankStatementParser : IBankStatementParser
{
    // How many leading rows to scan for the column header before giving up.
    private const int HeaderSearchRows = 30;

    public BankStatementParseResult Parse(byte[] content, string contentType, string fileName)
    {
        if (content is null || content.Length == 0)
        {
            return BankStatementParseResult.Empty("The uploaded statement was empty.");
        }

        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        var ct = (contentType ?? string.Empty).ToLowerInvariant();

        try
        {
            if (ext is ".csv" or ".tsv" || ct.Contains("csv"))
            {
                return ParseDelimited(content, ext == ".tsv" ? '\t' : ',');
            }

            if (ext is ".xlsx" or ".xlsm" || ct.Contains("spreadsheetml") || ct.Contains("ms-excel"))
            {
                return ParseExcel(content);
            }

            if (ext == ".pdf" || ct.Contains("pdf") || LooksLikePdf(content))
            {
                return ParsePdf(content);
            }

            if (LooksLikeZip(content))
            {
                // .xlsx is a zip container; an Office content-type may be missing on a raw upload.
                return ParseExcel(content);
            }

            // Last resort: treat as delimited text.
            return ParseDelimited(content, ',');
        }
        catch (Exception ex)
        {
            return BankStatementParseResult.Empty($"Could not parse '{fileName}': {ex.Message}");
        }
    }

    // ===================================================================== Excel

    private static BankStatementParseResult ParseExcel(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var workbook = new XLWorkbook(stream);

        var warnings = new List<string>();
        var grid = new List<string[]>();

        // Prefer a worksheet that actually contains a recognisable statement table.
        foreach (var ws in workbook.Worksheets)
        {
            var used = ws.RangeUsed();
            if (used is null)
            {
                continue;
            }

            var firstRow = used.FirstRow().RowNumber();
            var lastRow = used.LastRow().RowNumber();
            var firstCol = used.FirstColumn().ColumnNumber();
            var lastCol = used.LastColumn().ColumnNumber();

            var sheetGrid = new List<string[]>();
            for (var r = firstRow; r <= lastRow; r++)
            {
                var row = new string[lastCol - firstCol + 1];
                for (var c = firstCol; c <= lastCol; c++)
                {
                    var cell = ws.Cell(r, c);
                    // Emit dates in an unambiguous ISO form so the date parser never guesses the order.
                    row[c - firstCol] = cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var dt)
                        ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : cell.GetFormattedString().Trim();
                }

                sheetGrid.Add(row);
            }

            if (FindHeaderRow(sheetGrid) >= 0)
            {
                grid = sheetGrid;
                break;
            }

            // Remember the first non-empty sheet in case none has a clean header.
            if (grid.Count == 0)
            {
                grid = sheetGrid;
            }
        }

        return ExtractFromGrid(grid, warnings);
    }

    // ====================================================================== CSV

    private static BankStatementParseResult ParseDelimited(byte[] content, char delimiter)
    {
        var text = DecodeText(content);
        var grid = new List<string[]>();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            grid.Add(SplitDelimited(line, delimiter));
        }

        return ExtractFromGrid(grid, new List<string>());
    }

    // ====================================================================== PDF

    private static BankStatementParseResult ParsePdf(byte[] content)
    {
        var warnings = new List<string>();
        var textRows = new List<TextRow>();

        using (var document = PdfDocument.Open(content))
        {
            foreach (var page in document.GetPages())
            {
                textRows.AddRange(BuildRowsFromWords(page));
            }
        }

        if (textRows.Count == 0)
        {
            return BankStatementParseResult.Empty(
                "No selectable text was found in the PDF (it may be a scanned image — export the statement to Excel/CSV instead).");
        }

        // Locate the header row and use the X-position of each header word as a column anchor, then
        // bucket every later row's words into those columns to reconstruct a grid.
        var headerRowIndex = textRows.FindIndex(r => ClassifyHeaderScore(r.Cells.Select(c => c.Text).ToArray()) >= 3);
        if (headerRowIndex >= 0)
        {
            var anchors = textRows[headerRowIndex].Cells.Select(c => c.X).OrderBy(x => x).ToArray();
            var grid = new List<string[]> { BucketByAnchors(textRows[headerRowIndex], anchors) };
            for (var i = headerRowIndex + 1; i < textRows.Count; i++)
            {
                grid.Add(BucketByAnchors(textRows[i], anchors));
            }

            var gridResult = ExtractFromGrid(grid, warnings);
            if (gridResult.Lines.Count > 0)
            {
                return gridResult;
            }
        }

        // No usable column header: fall back to a per-line regex parse with balance-delta direction.
        warnings.Add("PDF columns could not be detected reliably; transactions were inferred line by line.");
        return ParsePdfFallback(textRows.Select(r => r.JoinedText).ToList(), warnings);
    }

    private static IEnumerable<TextRow> BuildRowsFromWords(Page page)
    {
        // Group words whose vertical centre lines up (within a tolerance) into a single visual row.
        const double yTolerance = 3.0;

        var words = page.GetWords()
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .Select(w => new PositionedWord(
                w.Text.Trim(),
                w.BoundingBox.Left,
                (w.BoundingBox.Top + w.BoundingBox.Bottom) / 2.0))
            .OrderByDescending(w => w.Y) // PDF origin is bottom-left: top of page first.
            .ToList();

        var rows = new List<TextRow>();
        foreach (var word in words)
        {
            var row = rows.FirstOrDefault(r => Math.Abs(r.Y - word.Y) <= yTolerance);
            if (row is null)
            {
                row = new TextRow(word.Y);
                rows.Add(row);
            }

            row.Add(word);
        }

        foreach (var row in rows)
        {
            row.Finish();
        }

        return rows;
    }

    private static string[] BucketByAnchors(TextRow row, double[] anchors)
    {
        var cells = new string[anchors.Length];
        var builders = new StringBuilder[anchors.Length];
        for (var i = 0; i < anchors.Length; i++)
        {
            builders[i] = new StringBuilder();
        }

        foreach (var word in row.Cells)
        {
            // Assign each word to the nearest column anchor.
            var best = 0;
            var bestDist = double.MaxValue;
            for (var i = 0; i < anchors.Length; i++)
            {
                var dist = Math.Abs(anchors[i] - word.X);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            if (builders[best].Length > 0)
            {
                builders[best].Append(' ');
            }

            builders[best].Append(word.Text);
        }

        for (var i = 0; i < anchors.Length; i++)
        {
            cells[i] = builders[i].ToString().Trim();
        }

        return cells;
    }

    private static BankStatementParseResult ParsePdfFallback(List<string> lines, List<string> warnings)
    {
        var parsed = new List<ParsedBankLine>();
        decimal? prevBalance = null;
        var rowIndex = 0;

        foreach (var line in lines)
        {
            var date = ExtractLeadingDate(line, out var rest);
            if (date is null)
            {
                continue; // statement table rows start with a date; skip headers/footers/wrapped text
            }

            // Trailing numbers on the line are (amount, balance) or just (amount).
            var numbers = TrailingNumbers(rest, out var narration);
            if (numbers.Count == 0)
            {
                continue;
            }

            decimal amount;
            decimal? balance = null;
            if (numbers.Count >= 2)
            {
                balance = numbers[^1];
                amount = numbers[^2];
            }
            else
            {
                amount = numbers[0];
            }

            // Direction from the balance delta when we have a running balance; else leave as a credit
            // guess and warn (a lone unsigned amount is genuinely ambiguous).
            decimal? debit = null, credit = null;
            if (balance is not null && prevBalance is not null)
            {
                if (balance < prevBalance)
                {
                    debit = Math.Abs(amount);
                }
                else
                {
                    credit = Math.Abs(amount);
                }
            }
            else
            {
                credit = Math.Abs(amount);
            }

            parsed.Add(new ParsedBankLine(++rowIndex, date, narration.Trim(), null, debit, credit, balance));
            prevBalance = balance ?? prevBalance;
        }

        if (parsed.Count == 0)
        {
            warnings.Add("No transaction rows could be recognised in the PDF.");
        }

        var (from, to) = PeriodOf(parsed);
        return new BankStatementParseResult(parsed, warnings, from, to);
    }

    // ============================================================ grid extraction

    private static BankStatementParseResult ExtractFromGrid(List<string[]> grid, List<string> warnings)
    {
        var headerIdx = FindHeaderRow(grid);
        if (headerIdx < 0)
        {
            warnings.Add("Could not find a transaction table header (expected columns like Date, Particulars, Withdrawal, Deposit, Balance).");
            return new BankStatementParseResult(Array.Empty<ParsedBankLine>(), warnings, null, null);
        }

        var map = MapColumns(grid[headerIdx]);
        var lines = new List<ParsedBankLine>();
        decimal? prevBalance = null;
        var rowIndex = 0;
        var skipped = 0;

        for (var r = headerIdx + 1; r < grid.Count; r++)
        {
            var row = grid[r];

            var date = map.Date >= 0 ? ParseDate(Cell(row, map.Date)) : null;
            var narration = BuildNarration(row, map);
            var reference = map.Reference >= 0 ? Cell(row, map.Reference) : null;
            var balance = map.Balance >= 0 ? ParseAmount(Cell(row, map.Balance)) : null;

            var debit = NonZero(map.Debit >= 0 ? ParseAmount(Cell(row, map.Debit)) : null);
            var credit = NonZero(map.Credit >= 0 ? ParseAmount(Cell(row, map.Credit)) : null);

            // Combined "Amount" column (optionally with a Dr/Cr indicator) when there are no
            // separate withdrawal/deposit columns.
            if (debit is null && credit is null && map.Amount >= 0)
            {
                var amt = ParseAmount(Cell(row, map.Amount));
                if (amt is not null && amt.Value != 0)
                {
                    var dir = map.DrCr >= 0 ? DirectionIndicator(Cell(row, map.DrCr)) : 0;
                    if (dir == 0)
                    {
                        dir = DirectionIndicator(Cell(row, map.Amount));
                    }

                    if (dir == 0 && amt.Value < 0)
                    {
                        dir = -1;
                    }

                    if (dir == 0 && balance is not null && prevBalance is not null)
                    {
                        dir = balance.Value < prevBalance.Value ? -1 : 1;
                    }

                    if (dir < 0)
                    {
                        debit = Math.Abs(amt.Value);
                    }
                    else
                    {
                        credit = Math.Abs(amt.Value);
                    }
                }
            }

            if (debit is null && credit is null)
            {
                skipped++;
                prevBalance = balance ?? prevBalance; // seed from an opening/closing balance band
                continue; // opening/closing-balance bands, totals, blank rows
            }

            lines.Add(new ParsedBankLine(++rowIndex, date, narration, NullIfEmpty(reference), debit, credit, balance));
            prevBalance = balance ?? prevBalance;
        }

        if (skipped > 0 && lines.Count > 0)
        {
            warnings.Add($"{skipped} non-transaction row(s) (totals / balance bands / blanks) were skipped.");
        }

        var (from, to) = PeriodOf(lines);
        return new BankStatementParseResult(lines, warnings, from, to);
    }

    private static string BuildNarration(string[] row, ColumnMap map)
    {
        var parts = new List<string>();
        if (map.Narration >= 0)
        {
            var n = Cell(row, map.Narration);
            if (n.Length > 0)
            {
                parts.Add(n);
            }
        }

        foreach (var extra in map.ExtraNarration)
        {
            var v = Cell(row, extra);
            if (v.Length > 0)
            {
                parts.Add(v);
            }
        }

        return string.Join(" ", parts).Trim();
    }

    // ======================================================== header / column map

    private sealed record ColumnMap(
        int Date,
        int Narration,
        int Reference,
        int Debit,
        int Credit,
        int Balance,
        int Amount,
        int DrCr,
        IReadOnlyList<int> ExtraNarration);

    private enum Role { None, Date, Narration, Reference, Debit, Credit, Balance, Amount, DrCr }

    private static int FindHeaderRow(List<string[]> grid)
    {
        var best = -1;
        var bestScore = 0;
        var limit = Math.Min(grid.Count, HeaderSearchRows);
        for (var r = 0; r < limit; r++)
        {
            var score = ClassifyHeaderScore(grid[r]);
            if (score > bestScore)
            {
                bestScore = score;
                best = r;
            }
        }

        // A real header carries at least three recognisable columns (e.g. Date + Particulars + Balance,
        // or Date + Withdrawal + Deposit).
        return bestScore >= 3 ? best : -1;
    }

    private static int ClassifyHeaderScore(string[] row)
    {
        var roles = new HashSet<Role>();
        foreach (var cell in row)
        {
            var role = ClassifyHeader(cell);
            if (role != Role.None)
            {
                roles.Add(role);
            }
        }

        var hasMoney = roles.Contains(Role.Debit) || roles.Contains(Role.Credit)
                       || roles.Contains(Role.Amount) || roles.Contains(Role.Balance);
        var hasAnchor = roles.Contains(Role.Date) || roles.Contains(Role.Narration);
        return hasMoney && hasAnchor ? roles.Count : 0;
    }

    private static ColumnMap MapColumns(string[] header)
    {
        int date = -1, narration = -1, reference = -1, debit = -1, credit = -1, balance = -1, amount = -1, drcr = -1;
        var extraNarration = new List<int>();

        for (var i = 0; i < header.Length; i++)
        {
            switch (ClassifyHeader(header[i]))
            {
                case Role.Date when date < 0: date = i; break;
                case Role.Narration:
                    if (narration < 0)
                    {
                        narration = i;
                    }
                    else
                    {
                        extraNarration.Add(i);
                    }

                    break;
                case Role.Reference when reference < 0: reference = i; break;
                case Role.Debit when debit < 0: debit = i; break;
                case Role.Credit when credit < 0: credit = i; break;
                case Role.Balance when balance < 0: balance = i; break;
                case Role.Amount when amount < 0: amount = i; break;
                case Role.DrCr when drcr < 0: drcr = i; break;
            }
        }

        return new ColumnMap(date, narration, reference, debit, credit, balance, amount, drcr, extraNarration);
    }

    private static Role ClassifyHeader(string cell)
    {
        var n = Normalize(cell);
        if (n.Length == 0)
        {
            return Role.None;
        }

        if (n.Contains("date"))
        {
            return Role.Date;
        }

        if (n.Contains("balance"))
        {
            return Role.Balance;
        }

        if (n.Contains("withdraw") || n.Contains("debit") || n.Contains("paidout") || n == "dr")
        {
            return Role.Debit;
        }

        if (n.Contains("deposit") || n.Contains("credit") || n.Contains("paidin") || n == "cr")
        {
            return Role.Credit;
        }

        if (n is "drcr" or "crdr" or "type" or "transactiontype" || n.Contains("indicator"))
        {
            return Role.DrCr;
        }

        if (n.Contains("amount") || n == "amt")
        {
            return Role.Amount;
        }

        if (n.Contains("ref") || n.Contains("cheque") || n.Contains("chq") || n.Contains("instrument") || n.Contains("utr"))
        {
            return Role.Reference;
        }

        if (n.Contains("narration") || n.Contains("particular") || n.Contains("description")
            || n.Contains("detail") || n.Contains("remark") || n.Contains("transaction"))
        {
            return Role.Narration;
        }

        return Role.None;
    }

    // ===================================================================== parsing

    private static readonly string[] DateFormats =
    {
        "dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy", "d/M/yyyy", "d-M-yyyy",
        "dd/MM/yy", "dd-MM-yy", "d/M/yy", "d-M-yy",
        "dd-MMM-yyyy", "dd MMM yyyy", "dd/MMM/yyyy", "dd-MMM-yy", "dd MMM yy",
        "yyyy-MM-dd", "yyyy/MM/dd"
    };

    private static DateOnly? ParseDate(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            return null;
        }

        if (DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return DateOnly.FromDateTime(exact);
        }

        // Day-first general fallback (Indian statements are dd/MM); en-GB makes the order explicit.
        if (DateTime.TryParse(s, CultureInfo.GetCultureInfo("en-GB"), DateTimeStyles.None, out var loose))
        {
            return DateOnly.FromDateTime(loose);
        }

        return null;
    }

    /// <summary>Parse a money cell, honouring ₹/Rs prefixes, thousands separators, parentheses and minus.</summary>
    private static decimal? ParseAmount(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        if (s.Length == 0 || s == "-")
        {
            return null;
        }

        var negative = false;
        if (s.StartsWith('(') && s.EndsWith(')'))
        {
            negative = true;
            s = s[1..^1];
        }

        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsDigit(c) || c == '.')
            {
                sb.Append(c);
            }
            else if (c == '-')
            {
                negative = true;
            }
        }

        if (sb.Length == 0)
        {
            return null;
        }

        if (!decimal.TryParse(sb.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        return negative ? -amount : amount;
    }

    /// <summary>+1 = credit, -1 = debit, 0 = unknown, read from a Dr/Cr indicator or inline suffix.</summary>
    private static int DirectionIndicator(string? value)
    {
        var n = Normalize(value ?? string.Empty);
        if (n.Length == 0)
        {
            return 0;
        }

        if (n is "dr" or "debit" || n.EndsWith("dr") || n.Contains("withdraw") || n.Contains("paidout"))
        {
            return -1;
        }

        if (n is "cr" or "credit" || n.EndsWith("cr") || n.Contains("deposit") || n.Contains("paidin"))
        {
            return 1;
        }

        return 0;
    }

    // ============================================================ PDF text helpers

    private static DateOnly? ExtractLeadingDate(string line, out string rest)
    {
        rest = line;
        var trimmed = line.TrimStart();
        var firstToken = trimmed.Split(' ', 2);
        if (firstToken.Length == 0)
        {
            return null;
        }

        var date = ParseDate(firstToken[0]);
        if (date is null)
        {
            return null;
        }

        rest = firstToken.Length > 1 ? firstToken[1] : string.Empty;
        return date;
    }

    /// <summary>Pull the trailing numeric tokens off a line; the remainder is the narration.</summary>
    private static List<decimal> TrailingNumbers(string text, out string narration)
    {
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var numbers = new List<decimal>();
        var lastTextIndex = tokens.Length - 1;

        for (var i = tokens.Length - 1; i >= 0; i--)
        {
            var amt = ParseAmount(tokens[i]);
            if (amt is not null && LooksNumeric(tokens[i]))
            {
                numbers.Insert(0, amt.Value);
                lastTextIndex = i - 1;
            }
            else
            {
                break;
            }
        }

        narration = lastTextIndex >= 0 ? string.Join(' ', tokens.Take(lastTextIndex + 1)) : string.Empty;
        return numbers;
    }

    private static bool LooksNumeric(string token)
        => token.Length > 0 && token.Count(char.IsDigit) >= 1
           && token.All(c => char.IsDigit(c) || c is '.' or ',' or '(' or ')' or '-');

    // ===================================================================== utility

    private static (DateOnly? From, DateOnly? To) PeriodOf(IReadOnlyList<ParsedBankLine> lines)
    {
        DateOnly? from = null, to = null;
        foreach (var line in lines)
        {
            if (line.Date is not { } d)
            {
                continue;
            }

            if (from is null || d < from)
            {
                from = d;
            }

            if (to is null || d > to)
            {
                to = d;
            }
        }

        return (from, to);
    }

    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }

    private static string Cell(string[] row, int index)
        => index >= 0 && index < row.Length ? (row[index] ?? string.Empty).Trim() : string.Empty;

    private static decimal? NonZero(decimal? v) => v is null || v.Value == 0m ? null : v;

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string[] SplitDelimited(string line, char delimiter)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
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
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                result.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        result.Add(sb.ToString().Trim());
        return result.ToArray();
    }

    private static string DecodeText(byte[] content)
    {
        // Strip a UTF-8 BOM if present, then decode as UTF-8.
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(content, 3, content.Length - 3);
        }

        return Encoding.UTF8.GetString(content);
    }

    private static bool LooksLikePdf(byte[] content)
        => content.Length >= 4 && content[0] == 0x25 && content[1] == 0x50 && content[2] == 0x44 && content[3] == 0x46;

    private static bool LooksLikeZip(byte[] content)
        => content.Length >= 2 && content[0] == 0x50 && content[1] == 0x4B; // "PK"

    private readonly record struct PositionedWord(string Text, double X, double Y);

    private sealed class TextRow
    {
        private readonly List<PositionedWord> _cells = new();

        public TextRow(double y) => Y = y;

        public double Y { get; }

        public IReadOnlyList<PositionedWord> Cells => _cells;

        public string JoinedText { get; private set; } = string.Empty;

        public void Add(PositionedWord word) => _cells.Add(word);

        public void Finish()
        {
            _cells.Sort((a, b) => a.X.CompareTo(b.X));
            JoinedText = string.Join(' ', _cells.Select(c => c.Text));
        }
    }
}
