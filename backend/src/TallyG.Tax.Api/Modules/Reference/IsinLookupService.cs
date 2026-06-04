using System.IO.Compression;

namespace TallyG.Tax.Api.Modules.Reference;

/// <summary>
/// ISIN → security lookup over the bundled master (<c>isin.tsv.gz</c>, embedded). The table (75k+ rows)
/// is decompressed and indexed ONCE process-wide via a lazy static, so even though Scrutor binds this
/// service scoped, no request re-parses it. Lookups are O(1) dictionary hits. Mirrors IfscLookupService.
/// </summary>
public sealed class IsinLookupService : IIsinLookupService
{
    private const string ResourceName = "isin.tsv.gz";

    private static readonly Lazy<IReadOnlyDictionary<string, IsinRecord>> Index =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public IsinRecord? Lookup(string isin)
    {
        var key = Normalize(isin);
        return key is not null && Index.Value.TryGetValue(key, out var rec) ? rec : null;
    }

    private static string? Normalize(string? isin)
        => string.IsNullOrWhiteSpace(isin) ? null : isin.Trim().ToUpperInvariant();

    private static IReadOnlyDictionary<string, IsinRecord> Load()
    {
        var asm = typeof(IsinLookupService).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded ISIN master '{ResourceName}' not found.");
        using var gz = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gz);

        var map = new Dictionary<string, IsinRecord>(80_000, StringComparer.OrdinalIgnoreCase);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            // ISIN \t NAME \t TYPE — split on the first two tabs (name/type are free text).
            var t1 = line.IndexOf('\t');
            if (t1 <= 0)
            {
                continue;
            }

            var t2 = line.IndexOf('\t', t1 + 1);
            var isin = line[..t1];
            var name = t2 < 0 ? line[(t1 + 1)..] : line[(t1 + 1)..t2];
            var type = t2 < 0 ? string.Empty : line[(t2 + 1)..];
            map[isin] = new IsinRecord(isin, name, type);
        }

        return map;
    }
}
