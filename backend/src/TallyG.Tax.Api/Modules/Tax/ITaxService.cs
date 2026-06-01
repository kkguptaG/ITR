namespace TallyG.Tax.Api.Modules.Tax;

/// <summary>
/// Application service for the Tax computation module (docs 03). Orchestrates the PURE
/// <see cref="TallyG.Tax.Domain.TaxEngine.ITaxCalculator"/>: fetches the active rule-set + the return's
/// data from the DB, runs the engine, and (for /tax/compute) persists the per-regime
/// <see cref="TallyG.Tax.Domain.Entities.TaxComputation"/> rows. The engine itself stays I/O-free.
/// </summary>
public interface ITaxService
{
    /// <summary>Compute and PERSIST the computation(s) for a saved return; returns both regimes.</summary>
    Task<ComputeResponse> ComputeAsync(ComputeRequest request, CancellationToken ct = default);

    /// <summary>Compare old vs new regime for a saved return (persists both, like compute).</summary>
    Task<ComputeResponse> RegimeCompareAsync(Guid returnId, CancellationToken ct = default);

    /// <summary>Ad-hoc calculator: compute from inline inputs with NO persistence.</summary>
    Task<RegimeComparisonDto> CalculateAsync(TaxCalculatorRequest request, CancellationToken ct = default);

    /// <summary>Return both regimes' slabs/limits for an assessment year (from the active rule-set).</summary>
    Task<SlabsResponse> GetSlabsAsync(string? assessmentYear, CancellationToken ct = default);

    /// <summary>80C/80D gap-analysis recommendations for a saved return or ad-hoc inputs.</summary>
    Task<RecommendationsResponse> RecommendAsync(RecommendationsRequest request, CancellationToken ct = default);
}
