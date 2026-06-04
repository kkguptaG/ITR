namespace TallyG.Tax.Api.Modules.Reference;

/// <summary>
/// NSE 31-Jan-2018 fair-market-value lookup (s.112A grandfathering, s.55(2)(ac)) over the bundled
/// master. Exact lookup by NSE symbol plus a small prefix search for an autocomplete.
/// </summary>
public interface IGrandfatherFmvLookupService
{
    /// <summary>The 31-Jan-2018 FMV for an NSE symbol (case-insensitive), or null if not listed then.</summary>
    GrandfatherFmvRecord? Lookup(string symbol);

    /// <summary>Up to <paramref name="limit"/> securities whose symbol starts with <paramref name="prefix"/>.</summary>
    IReadOnlyList<GrandfatherFmvRecord> Search(string prefix, int limit = 20);
}
