using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Common;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Reconciliation;

public interface IReconciliationService
{
    Task<ReconciliationReportDto> ReconcileAsync(Guid returnId, CancellationToken ct = default);
}

/// <summary>
/// Pre-filing reconciliation: compares what the assessee has fed under each head (salary, interest,
/// dividend, TDS, advance tax) against what the department reports in the latest uploaded AIS + Form 26AS
/// (their extracted fields). Surfaces under-reporting — the main cause of a §143(1) mismatch / notice —
/// before the return is filed. Owner/tenant-scoped. Scrutor binds it scoped (ReconciliationService :
/// IReconciliationService).
/// </summary>
public sealed class ReconciliationService : IReconciliationService
{
    private const decimal Tolerance = 100m;   // ignore sub-₹100 rounding differences

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ReconciliationService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ReconciliationReportDto> ReconcileAsync(Guid returnId, CancellationToken ct = default)
    {
        var ret = await _db.TaxReturns.FirstOrDefaultAsync(
                      r => r.Id == returnId && r.TenantId == _currentUser.TenantId && r.UserId == _currentUser.UserId, ct)
                  ?? throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");

        var ais = await LatestFieldMapAsync(returnId, DocumentKind.AIS, ct);
        var as26 = await LatestFieldMapAsync(returnId, DocumentKind.Form26AS, ct);

        if (ais.Count == 0 && as26.Count == 0)
        {
            return new ReconciliationReportDto(false, Array.Empty<ReconLineDto>(), 0, 0,
                "Upload your AIS and Form 26AS (Documents) and approve their extraction to reconcile this return against the department's records before filing.");
        }

        // ---- what the return declares ----
        var salaries = await _db.SalaryDetails.Where(s => s.TaxReturnId == returnId).ToListAsync(ct);
        var others = await _db.IncomeSources
            .Where(s => s.TaxReturnId == returnId && s.Type == IncomeType.OtherSources).ToListAsync(ct);
        var grossSalary = salaries.Sum(s => s.Gross + s.Perquisites + s.ProfitsInLieu);
        var savings = OtherByNature(others, "savings_interest");
        var fd = OtherByNature(others, "fd_interest");
        var dividend = OtherByNature(others, "dividend");

        var lines = new List<ReconLineDto>();
        void Compare(string category, string label, decimal inReturn, IReadOnlyDictionary<string, decimal> src, string srcKey, string srcName)
        {
            if (!src.TryGetValue(srcKey, out var inSource))
            {
                return;   // the department source doesn't report this line
            }

            if (inReturn <= 0m && inSource <= 0m)
            {
                return;
            }

            var (status, note) = Classify(inReturn, inSource);
            lines.Add(new ReconLineDto(category, label, inReturn, inSource, srcName, status, note));
        }

        Compare("salary", "Salary (gross)", grossSalary, ais, "ais.salary_gross", "AIS");
        Compare("interest", "Interest — savings bank", savings, ais, "ais.interest_savings_bank", "AIS");
        Compare("interest", "Interest — term deposit", fd, ais, "ais.interest_term_deposit", "AIS");
        Compare("dividend", "Dividend", dividend, ais, "ais.dividend_income", "AIS");

        // 26AS prepaid taxes: TDS (salary + non-salary) and advance tax. The return's TdsPaid is the
        // rolled-up credit; 26AS reports the deducted amounts.
        var tds26 = Get(as26, "form26as.tds_salary") + Get(as26, "form26as.tds_interest");
        if (tds26 > 0m || ret.TdsPaid > 0m)
        {
            var (status, note) = Classify(ret.TdsPaid, tds26, claimVsAvailable: true);
            lines.Add(new ReconLineDto("tds", "TDS credit", ret.TdsPaid, tds26, "26AS", status, note));
        }

        Compare("advance_tax", "Advance tax", ret.AdvanceTaxPaid, as26, "form26as.advance_tax", "26AS");

        var under = lines.Count(l => l.Status == "under_reported");
        var mismatches = lines.Count(l => l.Status != "matched");
        var notice = mismatches == 0
            ? "Your return matches the department's AIS / 26AS within rounding. Good to file."
            : $"{mismatches} line(s) differ from AIS/26AS ({under} under-reported). Review before filing — under-reported income is the leading cause of a §143(1) intimation.";

        return new ReconciliationReportDto(true, lines, mismatches, under, notice);
    }

    private static (string Status, string Note) Classify(decimal inReturn, decimal inSource, bool claimVsAvailable = false)
    {
        var diff = inReturn - inSource;
        if (Math.Abs(diff) <= Tolerance)
        {
            return ("matched", "Matches the department's records.");
        }

        if (inReturn < inSource)
        {
            // For income this is under-reporting; for TDS it means you haven't claimed all the credit.
            return claimVsAvailable
                ? ("under_reported", $"₹{inSource - inReturn:N0} of TDS in 26AS is not yet claimed — add the missing deductor(s) so you don't lose the credit.")
                : ("under_reported", $"₹{inSource - inReturn:N0} more is reported to the department than in your return — add it or you may get a mismatch notice.");
        }

        return ("over_reported", claimVsAvailable
            ? $"You are claiming ₹{diff:N0} more TDS than 26AS shows — the excess may be disallowed."
            : $"Your return shows ₹{diff:N0} more than the department's records (often fine — e.g. income they didn't capture).");
    }

    private async Task<IReadOnlyDictionary<string, decimal>> LatestFieldMapAsync(Guid returnId, DocumentKind kind, CancellationToken ct)
    {
        var json = await (from e in _db.DocumentExtractions
                          join d in _db.Documents on e.DocumentId equals d.Id
                          where d.TaxReturnId == returnId && d.TenantId == _currentUser.TenantId && d.Kind == kind
                          orderby e.CreatedAt descending
                          select e.FieldsJson).FirstOrDefaultAsync(ct);

        return ParseMoneyFields(json);
    }

    /// <summary>Parse the {key:{value,confidence,source}} extraction map into key → money value.</summary>
    private static IReadOnlyDictionary<string, decimal> ParseMoneyFields(string? json)
    {
        var map = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return map;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return map;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object
                    && prop.Value.TryGetProperty("value", out var v)
                    && v.ValueKind == JsonValueKind.String
                    && decimal.TryParse(v.GetString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amt))
                {
                    map[prop.Name] = amt;
                }
            }
        }
        catch (JsonException)
        {
            // tolerate a malformed map → no source figures
        }

        return map;
    }

    private static decimal OtherByNature(IEnumerable<IncomeSource> others, string nature)
        => others.Where(o => string.Equals(TaxComputationInputFactory.ExtractNature(o.SourceMetaJson), nature, StringComparison.OrdinalIgnoreCase))
                 .Sum(o => o.Amount);

    private static decimal Get(IReadOnlyDictionary<string, decimal> map, string key)
        => map.TryGetValue(key, out var v) ? v : 0m;
}
