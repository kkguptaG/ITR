using System.IO.Compression;

namespace TallyG.Tax.Api.Modules.BankAccounts;

/// <summary>
/// IFSC → bank/branch lookup over the bundled RBI master (<c>ifsc.tsv.gz</c>, embedded). The table
/// (170k+ rows) is decompressed and indexed ONCE process-wide via a lazy static, so even though Scrutor
/// binds this service scoped, no request re-parses it. Lookups are O(1) dictionary hits.
/// </summary>
public sealed class IfscLookupService : IIfscLookupService
{
    private const string ResourceName = "ifsc.tsv.gz";

    // Process-wide, thread-safe, built on first access. Keyed by upper-cased IFSC.
    private static readonly Lazy<IReadOnlyDictionary<string, IfscRecord>> Index =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public IfscRecord? Lookup(string ifsc)
    {
        var key = Normalize(ifsc);
        return key is not null && Index.Value.TryGetValue(key, out var rec) ? rec : null;
    }

    public bool Exists(string ifsc)
    {
        var key = Normalize(ifsc);
        return key is not null && Index.Value.ContainsKey(key);
    }

    private static string? Normalize(string? ifsc)
    {
        if (string.IsNullOrWhiteSpace(ifsc))
        {
            return null;
        }

        return ifsc.Trim().ToUpperInvariant();
    }

    private static IReadOnlyDictionary<string, IfscRecord> Load()
    {
        var asm = typeof(IfscLookupService).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded IFSC master '{ResourceName}' not found.");
        using var gz = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gz);

        // Pre-sized for ~171k rows to avoid rehashing during the load.
        var map = new Dictionary<string, IfscRecord>(180_000, StringComparer.OrdinalIgnoreCase);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            // IFSC \t BANK \t BRANCH — split on the first two tabs only (branch names are free text).
            var t1 = line.IndexOf('\t');
            if (t1 <= 0)
            {
                continue;
            }

            var t2 = line.IndexOf('\t', t1 + 1);
            if (t2 < 0)
            {
                continue;
            }

            var ifsc = line[..t1];
            var bank = line[(t1 + 1)..t2];
            var branch = line[(t2 + 1)..];
            map[ifsc] = new IfscRecord(ifsc, bank, branch);
        }

        return map;
    }
}
