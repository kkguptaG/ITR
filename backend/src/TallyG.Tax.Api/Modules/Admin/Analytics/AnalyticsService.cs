using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Admin.Analytics;

/// <summary>
/// Back-office analytics aggregations. Tenant-scoped for Admin (own tenant); SuperAdmin reads
/// across all tenants. Revenue is computed from captured (Paid) payments only — gross = amount
/// charged, GST = the tax component, net = gross − GST (our recognised fee). Date bucketing is
/// done in memory over the bounded query window so the same code path works on Sqlite and Postgres.
/// No manual DI — Scrutor binds AnalyticsService : IAnalyticsService scoped.
/// </summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private static readonly TimeSpan Ist = TimeSpan.FromHours(5.5);

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;

    public AnalyticsService(AppDbContext db, ICurrentUser currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    // ------------------------------------------------------------------ revenue

    public async Task<RevenueReportDto> GetRevenueAsync(
        string? granularity, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken ct = default)
    {
        var grain = NormalizeGranularity(granularity);
        var to = toUtc ?? _clock.UtcNow;
        var from = fromUtc ?? to.AddMonths(-12);

        if (from > to)
        {
            throw AppException.Validation("'from' must be on or before 'to'.", "ANALYTICS.RANGE_INVALID");
        }

        // Pull just the columns we need for captured payments in the window.
        var rows = await ScopedPayments()
            .Where(p => p.Status == PaymentStatus.Paid && p.CreatedAt >= from && p.CreatedAt <= to)
            .Select(p => new { p.CreatedAt, p.Amount, p.TaxGst })
            .ToListAsync(ct);

        var buckets = rows
            .GroupBy(r => BucketKey(r.CreatedAt, grain))
            .Select(g => new RevenuePointDto(
                Period: g.Key,
                GrossAmount: Money(g.Sum(x => x.Amount)),
                GstAmount: Money(g.Sum(x => x.TaxGst)),
                NetAmount: Money(g.Sum(x => x.Amount - x.TaxGst)),
                PaymentCount: g.LongCount()))
            .OrderBy(p => p.Period, StringComparer.Ordinal)
            .ToList();

        return new RevenueReportDto(
            Granularity: grain,
            FromUtc: from,
            ToUtc: to,
            TotalGross: Money(rows.Sum(x => x.Amount)),
            TotalGst: Money(rows.Sum(x => x.TaxGst)),
            TotalNet: Money(rows.Sum(x => x.Amount - x.TaxGst)),
            TotalPayments: rows.Count,
            Series: buckets);
    }

    // ------------------------------------------------------------------ filings

    public async Task<FilingsReportDto> GetFilingsAsync(CancellationToken ct = default)
    {
        var returns = ScopedReturns();

        var total = await returns.LongCountAsync(ct);

        var byStatus = await returns
            .GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        // ItrType is nullable (unclassified drafts); bucket those under "Unclassified".
        var byItr = await returns
            .GroupBy(r => r.ItrType)
            .Select(g => new { g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        var byRegime = await returns
            .GroupBy(r => r.Regime)
            .Select(g => new { g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        var filed = byStatus
            .Where(s => s.Key is ReturnStatus.Filed or ReturnStatus.Processed)
            .Sum(s => s.Count);

        return new FilingsReportDto(
            TotalReturns: total,
            FiledReturns: filed,
            ByStatus: byStatus
                .OrderBy(s => s.Key)
                .Select(s => new CountBucketDto(s.Key.ToString(), s.Count))
                .ToList(),
            ByItrType: byItr
                .OrderBy(s => s.Key)
                .Select(s => new CountBucketDto(s.Key?.ToString() ?? "Unclassified", s.Count))
                .ToList(),
            ByRegime: byRegime
                .OrderBy(s => s.Key)
                .Select(s => new CountBucketDto(s.Key?.ToString() ?? "Unset", s.Count))
                .ToList());
    }

    // ----------------------------------------------------------------- overview

    public async Task<AnalyticsOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var users = ScopedUsers();
        var returns = ScopedReturns();
        var payments = ScopedPayments();

        var now = _clock.UtcNow;
        var monthStartIst = StartOfMonthUtc(now);

        var totalUsers = await users.LongCountAsync(ct);
        var activeUsers = await users.LongCountAsync(u => u.Status == UserStatus.Active, ct);

        var totalReturns = await returns.LongCountAsync(ct);
        var inProgress = await returns.LongCountAsync(
            r => r.Status == ReturnStatus.InProgress || r.Status == ReturnStatus.Draft, ct);
        var filed = await returns.LongCountAsync(
            r => r.Status == ReturnStatus.Filed || r.Status == ReturnStatus.Processed, ct);
        var underReview = await returns.LongCountAsync(r => r.Status == ReturnStatus.UnderCaReview, ct);

        // Lifetime + this-month net revenue from captured payments (two small aggregate scans).
        var paid = payments.Where(p => p.Status == PaymentStatus.Paid);
        var paidCount = await paid.LongCountAsync(ct);
        var lifetimeGross = await paid.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var lifetimeGst = await paid.SumAsync(p => (decimal?)p.TaxGst, ct) ?? 0m;

        var monthPaid = paid.Where(p => p.CreatedAt >= monthStartIst);
        var monthGross = await monthPaid.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var monthGst = await monthPaid.SumAsync(p => (decimal?)p.TaxGst, ct) ?? 0m;

        var docsAwaiting = await ScopedDocumentsAwaitingReview().LongCountAsync(ct);
        var openLeads = await ScopedLeads()
            .LongCountAsync(l => l.Stage != LeadStage.Converted && l.Stage != LeadStage.Lost, ct);

        return new AnalyticsOverviewDto(
            TotalUsers: totalUsers,
            ActiveUsers: activeUsers,
            TotalReturns: totalReturns,
            ReturnsInProgress: inProgress,
            ReturnsFiled: filed,
            ReturnsUnderCaReview: underReview,
            LifetimeRevenueNet: Money(lifetimeGross - lifetimeGst),
            RevenueThisMonthNet: Money(monthGross - monthGst),
            PaidPayments: paidCount,
            DocumentsAwaitingReview: docsAwaiting,
            OpenLeads: openLeads,
            GeneratedAtUtc: now);
    }

    // ============================================================== scoping

    private IQueryable<Domain.Entities.User> ScopedUsers()
        => AdminScope.IsCrossTenant(_currentUser)
            ? _db.Users
            : _db.Users.Where(u => u.TenantId == _currentUser.TenantId);

    private IQueryable<Domain.Entities.TaxReturn> ScopedReturns()
        => AdminScope.IsCrossTenant(_currentUser)
            ? _db.TaxReturns
            : _db.TaxReturns.Where(r => r.TenantId == _currentUser.TenantId);

    private IQueryable<Domain.Entities.Payment> ScopedPayments()
        => AdminScope.IsCrossTenant(_currentUser)
            ? _db.Payments
            : _db.Payments.Where(p => p.TenantId == _currentUser.TenantId);

    private IQueryable<Domain.Entities.Document> ScopedDocumentsAwaitingReview()
    {
        var q = _db.Documents.Where(d => d.Status == DocumentStatus.NeedsReview);
        return AdminScope.IsCrossTenant(_currentUser)
            ? q
            : q.Where(d => d.TenantId == _currentUser.TenantId);
    }

    private IQueryable<Domain.Entities.Lead> ScopedLeads()
        => AdminScope.IsCrossTenant(_currentUser)
            ? _db.Leads
            : _db.Leads.Where(l => l.TenantId == _currentUser.TenantId);

    // ============================================================== helpers

    private static string NormalizeGranularity(string? granularity)
    {
        var g = (granularity ?? "month").Trim().ToLowerInvariant();
        return g switch
        {
            "day" or "week" or "month" => g,
            _ => throw AppException.Validation(
                "granularity must be 'day', 'week' or 'month'.", "ANALYTICS.GRANULARITY_INVALID")
        };
    }

    /// <summary>Stable, sortable bucket key in IST for the given grain.</summary>
    private static string BucketKey(DateTimeOffset instant, string grain)
    {
        var ist = instant.ToOffset(Ist);
        return grain switch
        {
            "day" => ist.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "week" => IsoWeekKey(ist),
            _ => ist.ToString("yyyy-MM", CultureInfo.InvariantCulture)
        };
    }

    private static string IsoWeekKey(DateTimeOffset ist)
    {
        var week = ISOWeek.GetWeekOfYear(ist.DateTime);
        var year = ISOWeek.GetYear(ist.DateTime);
        return $"{year:D4}-W{week:D2}";
    }

    private static DateTimeOffset StartOfMonthUtc(DateTimeOffset nowUtc)
    {
        var ist = nowUtc.ToOffset(Ist);
        var firstIst = new DateTimeOffset(ist.Year, ist.Month, 1, 0, 0, 0, Ist);
        return firstIst.ToUniversalTime();
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
