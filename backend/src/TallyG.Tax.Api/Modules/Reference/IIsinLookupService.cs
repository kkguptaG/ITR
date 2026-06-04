namespace TallyG.Tax.Api.Modules.Reference;

/// <summary>
/// ISIN → security name/type lookup over the bundled master (75k+ securities). Read-only reference
/// data parsed once process-wide. Auto-registered scoped by Scrutor (IsinLookupService : IIsinLookupService).
/// </summary>
public interface IIsinLookupService
{
    /// <summary>The security for this ISIN (case-insensitive), or null if it isn't in the master.</summary>
    IsinRecord? Lookup(string isin);
}
