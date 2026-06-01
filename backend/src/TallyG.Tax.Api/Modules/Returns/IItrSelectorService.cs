using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Returns;

/// <summary>
/// The ITR-form auto-selector (docs 03 §3.2). Implements the <b>disqualification cascade</b>:
/// start at the simplest form (ITR-1) and escalate to the lowest-numbered form whose
/// constraints are all satisfied. A single disqualifier bumps the taxpayer up — they are
/// never "promoted downward".
///
/// Pure and deterministic: same flags ⇒ same verdict. Auto-registered scoped by Scrutor
/// (name pattern ItrSelectorService : IItrSelectorService).
/// </summary>
public interface IItrSelectorService
{
    /// <summary>
    /// Run the cascade over the supplied feature flags and return the recommended form plus
    /// the blocked forms and the minimal deciding flags (so the UI can explain "why not the
    /// simple form?").
    /// </summary>
    ItrSelectionVerdict Select(ItrSelectorInput input);

    /// <summary>
    /// Derive the feature flags from a return's persisted child rows (income sources, capital
    /// gains, business income, house properties) and answers, then run the cascade. Used by
    /// POST /returns/{id}:suggest-type. Tenant/ownership scoping is the caller's responsibility.
    /// </summary>
    Task<ItrSelectionVerdict> SuggestForReturnAsync(Guid taxReturnId, CancellationToken ct = default);
}
