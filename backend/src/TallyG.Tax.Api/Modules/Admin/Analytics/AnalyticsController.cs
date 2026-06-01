using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Admin.Analytics;

/// <summary>
/// Back-office analytics dashboards (docs 07 §7.9). Restricted to Admin/SuperAdmin (a tighter gate
/// than the rest of the console — revenue is sensitive). Admin sees their tenant; SuperAdmin all.
/// </summary>
[ApiController]
[Route("api/v1/admin/analytics")]
[Authorize(Roles = AdminScope.AnalyticsRoles)]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;

    public AnalyticsController(IAnalyticsService analytics) => _analytics = analytics;

    /// <summary>Captured-payment revenue by period (day|week|month) over an optional window.</summary>
    [HttpGet("revenue")]
    [ProducesResponseType(typeof(RevenueReportDto), StatusCodes.Status200OK)]
    public Task<RevenueReportDto> Revenue(
        [FromQuery] string? granularity = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken ct = default)
        => _analytics.GetRevenueAsync(granularity, from, to, ct);

    /// <summary>Filing funnel counts by status, ITR type and regime.</summary>
    [HttpGet("filings")]
    [ProducesResponseType(typeof(FilingsReportDto), StatusCodes.Status200OK)]
    public Task<FilingsReportDto> Filings(CancellationToken ct)
        => _analytics.GetFilingsAsync(ct);

    /// <summary>Headline KPIs for the console home.</summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(AnalyticsOverviewDto), StatusCodes.Status200OK)]
    public Task<AnalyticsOverviewDto> Overview(CancellationToken ct)
        => _analytics.GetOverviewAsync(ct);
}
