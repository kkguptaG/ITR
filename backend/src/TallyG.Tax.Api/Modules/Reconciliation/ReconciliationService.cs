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
        var rentReceived = await _db.HouseProperties
            .Where(h => h.TaxReturnId == returnId).SumAsync(h => (decimal?)h.AnnualRent, ct) ?? 0m;

        // Sale consideration of the AIS-tracked listed securities (equity / mutual funds) — what the SFT reports.
        var securitiesTypes = new[] { CapitalGainAssetType.ListedEquity, CapitalGainAssetType.EquityMutualFund, CapitalGainAssetType.DebtMutualFund };
        var securitiesSaleValue = await _db.CapitalGains
            .Where(g => g.TaxReturnId == returnId && securitiesTypes.Contains(g.AssetType))
            .SumAsync(g => (decimal?)g.SalePrice, ct) ?? 0m;

        // Sale consideration of immovable property (land/building) — what the registrar's SFT-012 reports.
        var immovableSaleValue = await _db.CapitalGains
            .Where(g => g.TaxReturnId == returnId && g.AssetType == CapitalGainAssetType.ImmovableProperty)
            .SumAsync(g => (decimal?)g.SalePrice, ct) ?? 0m;

        var inputs = new ReconciliationInputs(
            GrossSalary: salaries.Sum(s => s.Gross + s.Perquisites + s.ProfitsInLieu),
            SavingsInterest: OtherByNature(others, "savings_interest"),
            FdInterest: OtherByNature(others, "fd_interest"),
            OtherInterest: OtherByNature(others, "interest"),
            RefundInterest: OtherByNature(others, "refund_interest"),
            Dividend: OtherByNature(others, "dividend"),
            RentReceived: rentReceived,
            SecuritiesSaleValue: securitiesSaleValue,
            TdsPaid: ret.TdsPaid,
            AdvanceTaxPaid: ret.AdvanceTaxPaid,
            SelfAssessmentTaxPaid: ret.SelfAssessmentTaxPaid,
            TcsPaid: ret.TcsPaid,
            ImmovablePropertySaleValue: immovableSaleValue);

        return ReconciliationEngine.BuildReport(inputs, ais, as26);
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
}
