// Admin/CRM module — analytics DTOs.
// Public contract for the back-office analytics dashboards (docs 07 §7.9 KPIs/metrics).
// camelCase on the wire. Money is decimal; counts are long.

using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Admin.Analytics;

/// <summary>One bucket of revenue in the requested period grain.</summary>
public sealed record RevenuePointDto(
    string Period,      // e.g. "2026-05" (month), "2026-05-31" (day)
    decimal GrossAmount,
    decimal GstAmount,
    decimal NetAmount,
    long PaymentCount);

/// <summary>GET /admin/analytics/revenue response — series plus totals.</summary>
public sealed record RevenueReportDto(
    string Granularity, // day | week | month
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    decimal TotalGross,
    decimal TotalGst,
    decimal TotalNet,
    long TotalPayments,
    IReadOnlyList<RevenuePointDto> Series);

/// <summary>A labelled count (used for status / ITR-type breakdowns).</summary>
public sealed record CountBucketDto(string Key, long Count);

/// <summary>GET /admin/analytics/filings response — filing funnel counts.</summary>
public sealed record FilingsReportDto(
    long TotalReturns,
    long FiledReturns,
    IReadOnlyList<CountBucketDto> ByStatus,
    IReadOnlyList<CountBucketDto> ByItrType,
    IReadOnlyList<CountBucketDto> ByRegime);

/// <summary>GET /admin/analytics/overview response — headline KPIs for the console home.</summary>
public sealed record AnalyticsOverviewDto(
    long TotalUsers,
    long ActiveUsers,
    long TotalReturns,
    long ReturnsInProgress,
    long ReturnsFiled,
    long ReturnsUnderCaReview,
    decimal LifetimeRevenueNet,
    decimal RevenueThisMonthNet,
    long PaidPayments,
    long DocumentsAwaitingReview,
    long OpenLeads,
    DateTimeOffset GeneratedAtUtc);
