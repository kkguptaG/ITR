namespace TallyG.Tax.Api.Modules.Admin.Analytics;

/// <summary>
/// Back-office analytics (docs 07 §7.9). Aggregates revenue, the filing funnel, and headline KPIs.
/// Tenant-scoped for Admin; all-tenant for SuperAdmin. Revenue counts only captured (Paid) payments.
/// Auto-registered scoped by Scrutor (AnalyticsService : IAnalyticsService).
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// GET /admin/analytics/revenue — captured-payment revenue bucketed by <paramref name="granularity"/>
    /// ("day" | "week" | "month") over the window <paramref name="fromUtc"/>..<paramref name="toUtc"/>
    /// (defaults: last 12 months, monthly).
    /// </summary>
    Task<RevenueReportDto> GetRevenueAsync(string? granularity, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken ct = default);

    /// <summary>GET /admin/analytics/filings — return counts by status, ITR type and regime.</summary>
    Task<FilingsReportDto> GetFilingsAsync(CancellationToken ct = default);

    /// <summary>GET /admin/analytics/overview — headline KPIs for the console home.</summary>
    Task<AnalyticsOverviewDto> GetOverviewAsync(CancellationToken ct = default);
}
