using System.Globalization;
using System.IO.Compression;

namespace TallyG.Tax.Api.Modules.Reference;

/// <summary>
/// 31-Jan-2018 NSE FMV lookup over the bundled master (<c>share-fmv-31jan2018.tsv.gz</c>, embedded;
/// ~1.7k symbols). Parsed once process-wide via a lazy static. Exact lookups are O(1); the prefix
/// search is a linear scan over the (small) symbol-sorted list. Mirrors IfscLookupService.
/// </summary>
public sealed class GrandfatherFmvLookupService : IGrandfatherFmvLookupService
{
    private const string ResourceName = "share-fmv-31jan2018.tsv.gz";

    private static readonly Lazy<Data> Index = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private sealed record Data(
        IReadOnlyDictionary<string, GrandfatherFmvRecord> BySymbol,
        IReadOnlyList<GrandfatherFmvRecord> Sorted);

    public GrandfatherFmvRecord? Lookup(string symbol)
    {
        var key = Normalize(symbol);
        return key is not null && Index.Value.BySymbol.TryGetValue(key, out var rec) ? rec : null;
    }

    public IReadOnlyList<GrandfatherFmvRecord> Search(string prefix, int limit = 20)
    {
        var key = Normalize(prefix);
        if (key is null)
        {
            return Array.Empty<GrandfatherFmvRecord>();
        }

        var hits = new List<GrandfatherFmvRecord>(limit);
        foreach (var rec in Index.Value.Sorted)
        {
            if (rec.Symbol.StartsWith(key, StringComparison.Ordinal))
            {
                hits.Add(rec);
                if (hits.Count >= limit)
                {
                    break;
                }
            }
        }

        return hits;
    }

    private static string? Normalize(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();

    private static Data Load()
    {
        var asm = typeof(GrandfatherFmvLookupService).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded FMV master '{ResourceName}' not found.");
        using var gz = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gz);

        var map = new Dictionary<string, GrandfatherFmvRecord>(2_048, StringComparer.OrdinalIgnoreCase);
        var sorted = new List<GrandfatherFmvRecord>(2_048);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var tab = line.IndexOf('\t');
            if (tab <= 0)
            {
                continue;
            }

            var symbol = line[..tab];
            if (!decimal.TryParse(line[(tab + 1)..], NumberStyles.Number, CultureInfo.InvariantCulture, out var fmv))
            {
                continue;
            }

            var rec = new GrandfatherFmvRecord(symbol, fmv);
            map[symbol] = rec;
            sorted.Add(rec); // file is already symbol-sorted
        }

        return new Data(map, sorted);
    }
}
