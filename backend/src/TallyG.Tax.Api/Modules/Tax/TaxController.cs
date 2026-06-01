using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Tax;

/// <summary>
/// Tax computation endpoints (docs 03 §3.4–3.8). Thin actions delegating to <see cref="ITaxService"/>;
/// DTO records in/out; errors via <see cref="AppException"/> (rendered as RFC 7807 problem+json).
///
/// Routes:
///   POST /api/v1/tax/compute          — compute + PERSIST a saved return's computation (both regimes)
///   POST /api/v1/tax/regime-compare   — old vs new for a saved return (persists, like compute)
///   POST /api/v1/tax/calculator       — ad-hoc what-if from inline inputs (NO persistence)
///   GET  /api/v1/tax/slabs?ay=        — both regimes' slabs/limits for an AY (from the rule-set)
///   POST /api/v1/tax/recommendations  — 80C/80D gap-analysis advisor (saved return or ad-hoc)
///
/// The compute/compare/recommendations endpoints operate on the caller's own returns (the service
/// scopes every row to user + tenant). The calculator and slabs endpoints are stateless utilities
/// and allow anonymous access so the public landing-page tax calculator works pre-login.
/// </summary>
[ApiController]
[Route("api/v1/tax")]
[Authorize]
public sealed class TaxController : ControllerBase
{
    private readonly ITaxService _tax;

    public TaxController(ITaxService tax) => _tax = tax;

    /// <summary>Compute and persist the tax for a saved return (returns both regimes + recommendation).</summary>
    [HttpPost("compute")]
    [ProducesResponseType(typeof(ComputeResponse), StatusCodes.Status200OK)]
    public Task<ComputeResponse> Compute([FromBody] ComputeRequest request, CancellationToken ct)
        => _tax.ComputeAsync(request, ct);

    /// <summary>Old-vs-new regime comparison for a saved return (persists both computations).</summary>
    [HttpPost("regime-compare")]
    [ProducesResponseType(typeof(ComputeResponse), StatusCodes.Status200OK)]
    public Task<ComputeResponse> RegimeCompare([FromBody] RegimeCompareRequest request, CancellationToken ct)
        => _tax.RegimeCompareAsync(request.ReturnId, ct);

    /// <summary>Ad-hoc tax calculator (no persistence). Anonymous — powers the public calculator widget.</summary>
    [HttpPost("calculator")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegimeComparisonDto), StatusCodes.Status200OK)]
    public Task<RegimeComparisonDto> Calculator([FromBody] TaxCalculatorRequest request, CancellationToken ct)
        => _tax.CalculateAsync(request, ct);

    /// <summary>Both regimes' slabs/limits for an assessment year (defaults to the active AY).</summary>
    [HttpGet("slabs")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SlabsResponse), StatusCodes.Status200OK)]
    public Task<SlabsResponse> Slabs([FromQuery] string? ay, CancellationToken ct)
        => _tax.GetSlabsAsync(ay, ct);

    /// <summary>80C/80D gap-analysis recommendations for a saved return or ad-hoc inputs.</summary>
    [HttpPost("recommendations")]
    [ProducesResponseType(typeof(RecommendationsResponse), StatusCodes.Status200OK)]
    public Task<RecommendationsResponse> Recommendations([FromBody] RecommendationsRequest request, CancellationToken ct)
        => _tax.RecommendAsync(request, ct);
}

/// <summary>POST /tax/regime-compare body.</summary>
public sealed record RegimeCompareRequest(Guid ReturnId);
