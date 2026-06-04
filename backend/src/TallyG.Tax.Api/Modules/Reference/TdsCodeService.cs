namespace TallyG.Tax.Api.Modules.Reference;

/// <summary>
/// Loads the bundled TDS section-code master (<c>tds-codes.tsv</c>, embedded; 59 rows) once
/// process-wide via a lazy static. Small enough to keep uncompressed. Mirrors the other Reference lookups.
/// </summary>
public sealed class TdsCodeService : ITdsCodeService
{
    private const string ResourceName = "tds-codes.tsv";

    private static readonly Lazy<IReadOnlyList<TdsCodeRecord>> Codes =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public IReadOnlyList<TdsCodeRecord> All() => Codes.Value;

    private static IReadOnlyList<TdsCodeRecord> Load()
    {
        var asm = typeof(TdsCodeService).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded TDS-code master '{ResourceName}' not found.");
        using var reader = new StreamReader(stream);

        var list = new List<TdsCodeRecord>(64);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var t1 = line.IndexOf('\t');
            if (t1 <= 0)
            {
                continue;
            }

            var t2 = line.IndexOf('\t', t1 + 1);
            var code = line[..t1];
            var section = t2 < 0 ? line[(t1 + 1)..] : line[(t1 + 1)..t2];
            var desc = t2 < 0 ? string.Empty : line[(t2 + 1)..];
            list.Add(new TdsCodeRecord(code, section, desc));
        }

        return list;
    }
}
