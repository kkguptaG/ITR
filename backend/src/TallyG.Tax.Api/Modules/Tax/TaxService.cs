using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using TallyG.Tax.Infrastructure.Persistence;
using TallyG.Tax.Api.Common;

namespace TallyG.Tax.Api.Modules.Tax;

/// <summary>
/// Tax computation application service (docs 03). Follows the canonical Auth/Returns pattern:
/// constructor-injected dependencies, <see cref="AppException"/> for failures, DTO records in/out,
/// no manual DI registration (Scrutor binds TaxService : ITaxService scoped).
///
/// Responsibilities: load the AY-scoped <see cref="TaxRuleSet"/> + the return's heads, invoke the
/// pure <see cref="ITaxCalculator"/>, render the result (with trace) to DTOs, and — for /tax/compute
/// — persist one <see cref="TaxComputation"/> row per regime with the cheaper one flagged
/// IsRecommended. Reads are scoped to the current user + tenant (a return owned by someone else is
/// indistinguishable from absent → 404, docs 04 §4.5).
/// </summary>
public sealed class TaxService : ITaxService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly ITaxCalculator _calculator;
    private readonly ILogger<TaxService> _logger;

    public TaxService(
        AppDbContext db,
        ICurrentUser currentUser,
        IDateTime clock,
        ITaxCalculator calculator,
        ILogger<TaxService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _calculator = calculator;
        _logger = logger;
    }

    // =========================================================== /tax/compute

    public async Task<ComputeResponse> ComputeAsync(ComputeRequest request, CancellationToken ct = default)
    {
        var (ret, ayCode, rulesJson) = await LoadReturnContextAsync(request.ReturnId, ct);
        var input = await BuildInputFromReturnAsync(ret, ayCode, rulesJson, ct);

        var comparison = _calculator.Compare(input);
        await PersistComputationsAsync(ret, comparison, ct);

        var rs = RuleSet.Parse(rulesJson);
        return ToComputeResponse(ret.Id, ayCode, ret.RuleSetVersion, comparison, rs);
    }

    public Task<ComputeResponse> RegimeCompareAsync(Guid returnId, CancellationToken ct = default)
        => ComputeAsync(new ComputeRequest(returnId, null), ct);

    // =========================================================== /tax/calculator (ad-hoc)

    public Task<RegimeComparisonDto> CalculateAsync(TaxCalculatorRequest request, CancellationToken ct = default)
        => CalculateInternalAsync(request, ct);

    private async Task<RegimeComparisonDto> CalculateInternalAsync(TaxCalculatorRequest request, CancellationToken ct)
    {
        var (ayCode, rulesJson, version) = await ResolveActiveRuleSetAsync(request.AssessmentYear, ct);
        var input = BuildInputFromAdHoc(request, ayCode, version, rulesJson);

        var comparison = _calculator.Compare(input);
        var rs = RuleSet.Parse(rulesJson);
        return new RegimeComparisonDto(
            comparison.Recommended,
            comparison.SavingsVsAlternative,
            comparison.Reason,
            ToResultDto(comparison.Old),
            ToResultDto(comparison.New),
            rs.IsProvisional,
            rs.ValidationStatus,
            rs.Framework,
            rs.Disclaimer);
    }

    // =========================================================== /tax/slabs

    public async Task<SlabsResponse> GetSlabsAsync(string? assessmentYear, CancellationToken ct = default)
    {
        var (ayCode, rulesJson, version) = await ResolveActiveRuleSetAsync(assessmentYear, ct);
        var rs = RuleSet.Parse(rulesJson);

        return new SlabsResponse(
            ayCode,
            version,
            rs.Cess,
            ToRegimeSlabsDto(rs.Old),
            ToRegimeSlabsDto(rs.New));
    }

    // =========================================================== /tax/recommendations

    public async Task<RecommendationsResponse> RecommendAsync(RecommendationsRequest request, CancellationToken ct = default)
    {
        TaxComputationInput input;

        if (request.ReturnId is { } returnId)
        {
            var (ret, ayCode, rulesJson) = await LoadReturnContextAsync(returnId, ct);
            input = await BuildInputFromReturnAsync(ret, ayCode, rulesJson, ct);
        }
        else if (request.AdHoc is { } adHoc)
        {
            var (ayCode, rulesJson, version) = await ResolveActiveRuleSetAsync(adHoc.AssessmentYear, ct);
            input = BuildInputFromAdHoc(adHoc, ayCode, version, rulesJson);
        }
        else
        {
            throw AppException.Validation("Provide either a returnId or ad-hoc inputs.", "TAX.RECO_INPUT_REQUIRED");
        }

        var result = DeductionRecommender.Recommend(_calculator, input);

        return new RecommendationsResponse(
            result.OldRegimeTax,
            result.NewRegimeTax,
            result.RegimeSwitchBeatsDeductions,
            result.Headline,
            result.Suggestions.Select(s => new DeductionSuggestionDto(
                s.Rank, s.Section, s.Label, s.GapToInvest, s.MarginalTaxSaved,
                s.RoiPerRupee, s.LockInYears, s.Liquidity, s.UtilityNote)).ToList());
    }

    // =========================================================== persistence

    private async Task PersistComputationsAsync(TaxReturn ret, RegimeComparison comparison, CancellationToken ct)
    {
        // Replace any prior computations for this return so re-computing is idempotent and the
        // "latest" view (ReturnService.BuildDetail) reflects the current inputs.
        var existing = await _db.TaxComputations.Where(c => c.TaxReturnId == ret.Id).ToListAsync(ct);
        if (existing.Count > 0)
        {
            _db.TaxComputations.RemoveRange(existing);
        }

        var now = _clock.UtcNow;
        _db.TaxComputations.Add(ToEntity(ret, comparison.Old, comparison.Recommended == Regime.Old, now));
        _db.TaxComputations.Add(ToEntity(ret, comparison.New, comparison.Recommended == Regime.New, now));

        // Surface the computed state on the return header (does not regress a paid/filed return).
        if (ret.Status is ReturnStatus.Draft or ReturnStatus.InProgress)
        {
            ret.Status = ReturnStatus.ComputedReady;
        }

        // Persist the engine's chosen regime onto the return if the user has not pinned one.
        ret.Regime ??= comparison.Recommended;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Computed return {ReturnId}: old=₹{Old} new=₹{New} recommended={Reco}",
            ret.Id, comparison.Old.TotalTax, comparison.New.TotalTax, comparison.Recommended);
    }

    private TaxComputation ToEntity(TaxReturn ret, ComputationResult r, bool recommended, DateTimeOffset now) => new()
    {
        TenantId = ret.TenantId,
        TaxReturnId = ret.Id,
        Regime = r.Regime,
        GrossTotalIncome = r.GrossTotalIncome,
        TotalDeductions = r.TotalDeductions,
        TaxableIncome = r.TaxableIncome,
        // TaxBeforeCess on the entity == tax after rebate + surcharge, before cess.
        TaxBeforeCess = r.TaxBeforeRebate - r.Rebate87A + r.Surcharge,
        Cess = r.Cess,
        Rebate87A = r.Rebate87A,
        Surcharge = r.Surcharge,
        TotalTax = r.TotalTax,
        TdsPaid = r.TdsPaid,
        AdvanceTax = r.AdvanceTax,
        InterestPenalty = r.InterestPenalty,
        Interest234A = r.Interest234A,
        Interest234B = r.Interest234B,
        Interest234C = r.Interest234C,
        RefundOrPayable = r.RefundOrPayable,
        AdjustedTotalIncome = r.AdjustedTotalIncome,
        AlternativeMinimumTax = r.AlternativeMinimumTax,
        AmtCreditGenerated = r.AmtCreditGenerated,
        AmtCreditSetOff = r.AmtCreditSetOff,
        Relief89 = r.Relief89,
        Relief90And91 = r.Relief90And91,
        HousePropertyLossCarriedForward = r.HousePropertyLossCarriedForward,
        BusinessLossCarriedForward = r.BusinessLossCarriedForward,
        SpeculativeLossCarriedForward = r.SpeculativeLossCarriedForward,
        ShortTermCapitalLossCarriedForward = r.ShortTermCapitalLossCarriedForward,
        LongTermCapitalLossCarriedForward = r.LongTermCapitalLossCarriedForward,
        IsRecommended = recommended,
        TraceJson = JsonSerializer.Serialize(r.Trace, TraceJsonOptions),
        ComputedAt = now,
    };

    // =========================================================== input builders

    /// <summary>
    /// Load the return's heads from the DB and map to the engine's regime-agnostic input. Kept
    /// consistent with ReturnService.BuildComputationInput so /tax/compute and the filing snapshot
    /// agree on the figures. Pulls the assessee age from the linked UserProfile when present.
    /// </summary>
    private async Task<TaxComputationInput> BuildInputFromReturnAsync(
        TaxReturn ret, string ayCode, string rulesJson, CancellationToken ct)
    {
        var salaries = await _db.SalaryDetails.Where(s => s.TaxReturnId == ret.Id).ToListAsync(ct);
        var houses = await _db.HouseProperties.Where(h => h.TaxReturnId == ret.Id).ToListAsync(ct);
        var gains = await _db.CapitalGains.Where(c => c.TaxReturnId == ret.Id).ToListAsync(ct);
        var businesses = await _db.BusinessIncomes.Where(b => b.TaxReturnId == ret.Id).ToListAsync(ct);
        var incomeSources = await _db.IncomeSources.Where(s => s.TaxReturnId == ret.Id).ToListAsync(ct);
        var deductions = await _db.Deductions.Where(d => d.TaxReturnId == ret.Id).ToListAsync(ct);
        var donations80G = await _db.Donations80G.Where(d => d.TaxReturnId == ret.Id).ToListAsync(ct);
        var exemptIncomes = await _db.ExemptIncomes.Where(e => e.TaxReturnId == ret.Id).ToListAsync(ct);
        var foreignSourceIncomes = await _db.ForeignSourceIncomes.Where(f => f.TaxReturnId == ret.Id).ToListAsync(ct);

        var age = await ResolveAgeAsync(ret, ct);

        return TaxComputationInputFactory.FromReturn(
            ret, ayCode, rulesJson, age, DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
            salaries, houses, gains, businesses, incomeSources, deductions, donations80G, exemptIncomes, foreignSourceIncomes);
    }

    private static TaxComputationInput BuildInputFromAdHoc(
        TaxCalculatorRequest r, string ayCode, string version, string rulesJson)
    {
        return new TaxComputationInput
        {
            AssessmentYearCode = ayCode,
            RuleSetVersion = version,
            RulesJson = rulesJson,
            Age = r.Age,
            Salaries = (r.Salaries ?? Array.Empty<SalaryInputDto>()).Select(s => new SalaryInput(
                s.Employer ?? "Employer", s.Gross, s.Perquisites, s.ExemptAllowances, s.HraExemption, s.ProfessionalTax)).ToList(),
            HouseProperties = (r.HouseProperties ?? Array.Empty<HousePropertyInputDto>()).Select(h => new HousePropertyInput(
                h.Type, h.AnnualValue, h.MunicipalTaxesPaid, h.InterestOnLoan)).ToList(),
            CapitalGains = (r.CapitalGains ?? Array.Empty<CapitalGainInputDto>()).Select(c => new CapitalGainInput(
                c.AssetType, c.Term, c.TaxSection, c.SaleConsideration, c.CostOfAcquisition, c.CostOfImprovement,
                c.ExpensesOnTransfer, c.ExemptionAmount, c.AcquisitionDate, c.TransferDate,
                c.FairMarketValueOnGrandfatherDate, c.IndexedCost, c.ExemptionSection, c.ReinvestmentAmount)).ToList(),
            BusinessIncomes = (r.BusinessIncomes ?? Array.Empty<BusinessIncomeInputDto>()).Select(b => new BusinessIncomeInput(
                b.IsPresumptive, b.PresumptiveSection, b.Turnover, b.DigitalReceipts, b.CashReceipts, b.NetProfit, b.Speculative)).ToList(),
            OtherIncomes = (r.OtherIncomes ?? Array.Empty<OtherIncomeInputDto>()).Select(o => new OtherIncomeInput(o.Label, o.Amount, o.Nature)).ToList(),
            Deductions = (r.Deductions ?? Array.Empty<DeductionInputDto>()).Select(d => new DeductionInput(d.Section, d.ClaimedAmount, d.SubType)).ToList(),
            TdsPaid = r.TdsPaid,
            TcsPaid = r.TcsPaid,
            AdvanceTaxPaid = r.AdvanceTaxPaid,
            SelfAssessmentTaxPaid = r.SelfAssessmentTaxPaid,
            BroughtForwardHousePropertyLoss = r.BroughtForwardHousePropertyLoss,
            BroughtForwardBusinessLoss = r.BroughtForwardBusinessLoss,
            BroughtForwardShortTermCapitalLoss = r.BroughtForwardShortTermCapitalLoss,
            BroughtForwardLongTermCapitalLoss = r.BroughtForwardLongTermCapitalLoss,
            BroughtForwardAmtCredit = r.BroughtForwardAmtCredit,
            Relief89 = r.Relief89,
            ForeignIncomeDoublyTaxed = r.ForeignIncomeDoublyTaxed,
            ForeignTaxPaid = r.ForeignTaxPaid,
            ForeignDtaaApplies = r.ForeignDtaaApplies,
        };
    }

    // =========================================================== context loaders

    private async Task<(TaxReturn Return, string AyCode, string RulesJson)> LoadReturnContextAsync(Guid returnId, CancellationToken ct)
    {
        var ret = await _db.TaxReturns
                      .Include(r => r.AssessmentYear)
                      .FirstOrDefaultAsync(r => r.Id == returnId
                                                && r.TenantId == _currentUser.TenantId
                                                && r.UserId == _currentUser.UserId, ct)
                  ?? throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");

        var ayCode = ret.AssessmentYear?.Code ?? string.Empty;

        var rulesJson = await _db.TaxRuleSets
            .Where(rs => rs.AssessmentYearId == ret.AssessmentYearId && rs.Version == ret.RuleSetVersion)
            .Select(rs => rs.RulesJson)
            .FirstOrDefaultAsync(ct);

        // Fall back to the active rule-set for the AY if the pinned version is missing.
        if (string.IsNullOrWhiteSpace(rulesJson))
        {
            rulesJson = await _db.TaxRuleSets
                .Where(rs => rs.AssessmentYearId == ret.AssessmentYearId && rs.Status == RuleSetStatus.Active)
                .Select(rs => rs.RulesJson)
                .FirstOrDefaultAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(rulesJson))
        {
            throw new AppException("TAX.RULESET_MISSING",
                $"No tax rule-set is configured for {ayCode}.", 422);
        }

        return (ret, ayCode, rulesJson);
    }

    private async Task<(string AyCode, string RulesJson, string Version)> ResolveActiveRuleSetAsync(string? ayCode, CancellationToken ct)
    {
        var code = (ayCode ?? string.Empty).Trim();

        var ayQuery = _db.AssessmentYears.AsQueryable();
        AssessmentYear? ay = code.Length > 0
            ? await ayQuery.FirstOrDefaultAsync(a => a.Code == code, ct)
            : await ayQuery.Where(a => a.IsActive).FirstOrDefaultAsync(ct);

        if (ay is null)
        {
            throw AppException.Validation(
                code.Length > 0 ? $"Unknown assessment year '{code}'." : "No active assessment year is configured.",
                "TAX.AY_UNKNOWN");
        }

        // Prefer the AY's pinned active version; else any active rule-set for the AY.
        var ruleSet = await _db.TaxRuleSets
                          .Where(rs => rs.AssessmentYearId == ay.Id && rs.Version == ay.RuleSetVersion)
                          .Select(rs => new { rs.RulesJson, rs.Version })
                          .FirstOrDefaultAsync(ct)
                      ?? await _db.TaxRuleSets
                          .Where(rs => rs.AssessmentYearId == ay.Id && rs.Status == RuleSetStatus.Active)
                          .Select(rs => new { rs.RulesJson, rs.Version })
                          .FirstOrDefaultAsync(ct);

        if (ruleSet is null || string.IsNullOrWhiteSpace(ruleSet.RulesJson))
        {
            throw new AppException("TAX.RULESET_MISSING", $"No tax rule-set is configured for {ay.Code}.", 422);
        }

        return (ay.Code, ruleSet.RulesJson, ruleSet.Version);
    }

    private async Task<int> ResolveAgeAsync(TaxReturn ret, CancellationToken ct)
    {
        // Age at year-end drives senior/super-senior slabs. Derived from the UserProfile DOB
        // against the AY end date; defaults to an adult (<60) slab when DOB is absent.
        var dob = await _db.UserProfiles
            .Where(p => p.UserId == ret.UserId)
            .Select(p => p.Dob)
            .FirstOrDefaultAsync(ct);

        var yearEnd = ret.AssessmentYear?.EndDate ?? new DateOnly(DateTime.UtcNow.Year, 3, 31);
        if (dob is { } d && d != default)
        {
            var age = yearEnd.Year - d.Year;
            if (d > yearEnd.AddYears(-age))
            {
                age--;
            }

            return Math.Max(0, age);
        }

        return 30;
    }

    // =========================================================== DTO mapping

    private static ComputeResponse ToComputeResponse(Guid returnId, string ayCode, string version, RegimeComparison c, RuleSet rs)
        => new(
            returnId,
            ayCode,
            version,
            c.Recommended,
            c.SavingsVsAlternative,
            c.Reason,
            ToResultDto(c.Old),
            ToResultDto(c.New),
            rs.IsProvisional,
            rs.ValidationStatus,
            rs.Framework,
            rs.Disclaimer);

    private static TaxComputationResultDto ToResultDto(ComputationResult r)
        => new(
            r.Regime,
            r.GrossTotalIncome,
            r.TotalDeductions,
            r.TaxableIncome,
            r.TaxBeforeRebate,
            r.Rebate87A,
            r.Surcharge,
            r.Cess,
            r.TotalTax,
            r.TdsPaid,
            r.AdvanceTax,
            r.InterestPenalty,
            r.RefundOrPayable,
            r.AdjustedTotalIncome,
            r.AlternativeMinimumTax,
            r.AmtCreditGenerated,
            r.AmtCreditSetOff,
            r.Relief89,
            r.Relief90And91,
            r.HousePropertyLossCarriedForward,
            r.BusinessLossCarriedForward,
            r.SpeculativeLossCarriedForward,
            r.ShortTermCapitalLossCarriedForward,
            r.LongTermCapitalLossCarriedForward,
            r.Trace.Select(t => new TraceLineDto(t.Step, t.Description, t.Amount, t.RuleRef)).ToList());

    private static RegimeSlabsDto ToRegimeSlabsDto(RegimeRules rr)
        => new(
            rr.IsDefault,
            rr.StdDeductionSalary,
            rr.Slabs.Select(s => new SlabBandDto(s.Upto, s.Rate)).ToList(),
            rr.Rebate87A is { } rb ? new RebateDto(rb.IncomeThreshold, rb.MaxRebate, rb.MarginalRelief) : null,
            rr.SurchargeBands
                .OrderBy(b => b.Above)
                .Select(b => new SurchargeBandDto(b.Above, b.Rate)).ToList());

    private static readonly JsonSerializerOptions TraceJsonOptions = new() { WriteIndented = false };
}
