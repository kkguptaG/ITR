using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Payments;

/// <summary>
/// Gapless per-FY invoice numbering. GST law requires a continuous serial within a financial year;
/// a dedicated SERIAL would leave gaps on rollback, so we derive the next number from the current
/// max for the FY prefix (an "advisory"/counter approach over the existing rows). The caller wraps
/// the insert + the unique invoice-number index in a retry so a rare race re-reads the max and
/// re-issues — guaranteeing no duplicate and no gap.
/// </summary>
public sealed class InvoiceNumberService : IInvoiceNumberService
{
    private const string Series = "TG";

    private readonly AppDbContext _db;

    public InvoiceNumberService(AppDbContext db) => _db = db;

    public async Task<string> NextAsync(DateTimeOffset issuedAt, CancellationToken ct = default)
    {
        var prefix = $"{Series}/{FinancialYear(issuedAt)}/";

        // Pull the numeric suffixes for this FY and take max+1. The set is tiny (one FY of our
        // own invoices) so this is cheap; for Postgres scale a sequence-per-FY table would replace it.
        var numbers = await _db.Invoices
            .Where(i => i.Number.StartsWith(prefix))
            .Select(i => i.Number)
            .ToListAsync(ct);

        var next = 1;
        foreach (var number in numbers)
        {
            var suffix = number[prefix.Length..];
            if (int.TryParse(suffix, out var seq) && seq >= next)
            {
                next = seq + 1;
            }
        }

        return prefix + next.ToString("D5");
    }

    /// <summary>
    /// Financial year (Apr 1 – Mar 31) label, e.g. an Aug-2025 date → "2025-26", a Feb-2026 → "2025-26".
    /// Derived in IST per the API conventions (docs 04 §4.1 "FY derived server-side").
    /// </summary>
    internal static string FinancialYear(DateTimeOffset instant)
    {
        // Render the instant in IST before deciding the FY boundary.
        var ist = instant.ToOffset(TimeSpan.FromHours(5.5));
        var startYear = ist.Month >= 4 ? ist.Year : ist.Year - 1;
        var endYear = (startYear + 1) % 100;
        return $"{startYear}-{endYear:D2}";
    }
}
