namespace TallyG.Tax.Api.Modules.Reference;

/// <summary>
/// The ITD TDS deductee/section codes (the small fixed master from the e-filing utility), for the
/// section picker on a TDS-credit entry. Parsed once process-wide. Auto-registered scoped by Scrutor.
/// </summary>
public interface ITdsCodeService
{
    /// <summary>All TDS section codes, in the canonical (section) order of the master.</summary>
    IReadOnlyList<TdsCodeRecord> All();
}
