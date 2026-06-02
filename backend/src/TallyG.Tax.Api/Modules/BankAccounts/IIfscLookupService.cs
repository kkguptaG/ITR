namespace TallyG.Tax.Api.Modules.BankAccounts;

/// <summary>
/// Looks up a bank + branch for an IFSC against the bundled RBI master (170k+ codes). Read-only
/// reference data; the table is parsed once process-wide and shared. Auto-registered scoped by Scrutor
/// (IfscLookupService : IIfscLookupService) but the heavy table is a process-wide singleton internally.
/// </summary>
public interface IIfscLookupService
{
    /// <summary>The bank/branch for this IFSC (case-insensitive), or null if it isn't in the master.</summary>
    IfscRecord? Lookup(string ifsc);

    /// <summary>Whether this IFSC exists in the master (cheaper than <see cref="Lookup"/> when the value is unused).</summary>
    bool Exists(string ifsc);
}
