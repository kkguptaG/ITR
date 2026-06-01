using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using TallyG.Tax.Infrastructure.Persistence;
using TallyG.Tax.Api.Common;

namespace TallyG.Tax.Api.Modules.Returns;

/// <summary>
/// Returns/Filing application service. Follows the canonical Auth pattern: constructor-injected
/// dependencies, <see cref="AppException"/> for domain/validation failures, DTO records in/out,
/// no manual DI registration (Scrutor binds ReturnService : IReturnService scoped).
///
/// Every read/write is scoped to <see cref="ICurrentUser.TenantId"/> + <see cref="ICurrentUser.UserId"/>
/// — a return belonging to another tenant or user is indistinguishable from "absent" (404, per
/// docs 04 §4.5). Child rows inherit the tenant id from their owning return.
/// </summary>
public sealed class ReturnService : IReturnService
{
    // Statutory deduction caps (illustrative — docs 03 §3.8.1). Used only for the completeness
    // validation pass; the authoritative capping lives in the rule-set-driven engine (docs 03).
    private static readonly IReadOnlyDictionary<string, decimal> SectionCaps =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["80C"] = 150_000m,
            ["80CCD(1B)"] = 50_000m,
            ["80D"] = 100_000m,
            ["80TTA"] = 10_000m,
            ["80TTB"] = 50_000m,
        };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly IEFilingClient _eFiling;
    private readonly IItrSelectorService _selector;
    private readonly ITaxCalculator _calculator;
    private readonly ILogger<ReturnService> _logger;

    public ReturnService(
        AppDbContext db,
        ICurrentUser currentUser,
        IDateTime clock,
        IEFilingClient eFiling,
        IItrSelectorService selector,
        ITaxCalculator calculator,
        ILogger<ReturnService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _eFiling = eFiling;
        _selector = selector;
        _calculator = calculator;
        _logger = logger;
    }

    // =========================================================== return header CRUD

    public async Task<ReturnDetailDto> CreateAsync(CreateReturnRequest request, CancellationToken ct = default)
    {
        var ayCode = (request.AssessmentYear ?? string.Empty).Trim();
        if (ayCode.Length == 0)
        {
            throw AppException.Validation("Assessment year is required.", "RETURN.AY_REQUIRED");
        }

        var ay = await _db.AssessmentYears.FirstOrDefaultAsync(a => a.Code == ayCode, ct)
                 ?? throw AppException.Validation($"Unknown assessment year '{ayCode}'.", "RETURN.AY_UNKNOWN");

        if (!ay.IsFilingOpen)
        {
            throw AppException.Validation($"Filing for {ay.Code} is closed.", "RETURN.AY_CLOSED");
        }

        // Pin-on-file: freeze the active rule-set + questionnaire versions at creation (docs 03 §3.11).
        var schemaVersion = await _db.QuestionnaireSchemas
            .Where(s => s.AssessmentYearId == ay.Id && s.Status == SchemaStatus.Active)
            .Select(s => s.Version)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var entity = new TaxReturn
        {
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            AssessmentYearId = ay.Id,
            ItrType = request.ItrType,
            Regime = request.Regime,
            Status = ReturnStatus.Draft,
            RuleSetVersion = ay.RuleSetVersion,
            QuestionnaireSchemaVersion = schemaVersion,
            AnswersJson = "{}",
            FilingMode = "self"
        };

        _db.TaxReturns.Add(entity);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Created return {ReturnId} for user {UserId} AY {Ay}", entity.Id, entity.UserId, ay.Code);

        return await BuildDetailAsync(entity.Id, ct);
    }

    public async Task<PagedResult<ReturnSummaryDto>> ListAsync(
        string? ay, string? status, string? itrType, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);

        var query = _db.TaxReturns
            .Where(r => r.TenantId == _currentUser.TenantId && r.UserId == _currentUser.UserId);

        if (!string.IsNullOrWhiteSpace(ay))
        {
            var ayCode = ay.Trim();
            query = query.Where(r => r.AssessmentYear!.Code == ayCode);
        }

        if (TryParseStatus(status, out var parsedStatus))
        {
            query = query.Where(r => r.Status == parsedStatus);
        }

        if (TryParseItrType(itrType, out var parsedItr))
        {
            query = query.Where(r => r.ItrType == parsedItr);
        }

        var total = await query.LongCountAsync(ct);

        // SQLite (the no-infra demo provider) cannot ORDER BY a DateTimeOffset column, so order
        // the newest-first page client-side there; Postgres keeps the efficient server-side sort.
        var projected = query.Select(r => new ReturnSummaryDto(
            r.Id,
            r.AssessmentYear!.Code,
            r.ItrType,
            r.Status,
            r.Regime,
            r.AcknowledgmentNumber,
            r.CreatedAt,
            r.SubmittedAt));

        List<ReturnSummaryDto> items;
        if (_db.Database.IsSqlite())
        {
            var all = await projected.ToListAsync(ct);
            items = all
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        else
        {
            items = await projected
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        return new PagedResult<ReturnSummaryDto>(items, page, pageSize, total);
    }

    public Task<ReturnDetailDto> GetAsync(Guid id, CancellationToken ct = default) => BuildDetailAsync(id, ct);

    public async Task<ReturnDetailDto> UpdateAsync(Guid id, UpdateReturnRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        if (request.ItrType.HasValue)
        {
            ret.ItrType = request.ItrType;
        }

        if (request.Regime.HasValue)
        {
            ret.Regime = request.Regime;
        }

        if (request.AnswersJson is not null)
        {
            ret.AnswersJson = NormalizeJson(request.AnswersJson, "RETURN.ANSWERS_INVALID");
        }

        // Prepaid taxes + brought-forward losses (only the supplied fields; clamped non-negative).
        if (request.TdsPaid is { } tds) ret.TdsPaid = Math.Max(0m, tds);
        if (request.TcsPaid is { } tcs) ret.TcsPaid = Math.Max(0m, tcs);
        if (request.AdvanceTaxPaid is { } adv) ret.AdvanceTaxPaid = Math.Max(0m, adv);
        if (request.SelfAssessmentTaxPaid is { } sa) ret.SelfAssessmentTaxPaid = Math.Max(0m, sa);
        if (request.BroughtForwardHousePropertyLoss is { } hpl) ret.BroughtForwardHousePropertyLoss = Math.Max(0m, hpl);
        if (request.BroughtForwardBusinessLoss is { } bl) ret.BroughtForwardBusinessLoss = Math.Max(0m, bl);
        if (request.BroughtForwardShortTermCapitalLoss is { } stcl) ret.BroughtForwardShortTermCapitalLoss = Math.Max(0m, stcl);
        if (request.BroughtForwardLongTermCapitalLoss is { } ltcl) ret.BroughtForwardLongTermCapitalLoss = Math.Max(0m, ltcl);
        // AMT credit (s.115JD) + reliefs (s.89/90/91).
        if (request.BroughtForwardAmtCredit is { } amtc) ret.BroughtForwardAmtCredit = Math.Max(0m, amtc);
        if (request.Relief89 is { } r89) ret.Relief89 = Math.Max(0m, r89);
        if (request.ForeignIncomeDoublyTaxed is { } fdi) ret.ForeignIncomeDoublyTaxed = Math.Max(0m, fdi);
        if (request.ForeignTaxPaid is { } ftp) ret.ForeignTaxPaid = Math.Max(0m, ftp);
        if (request.ForeignDtaaApplies is { } dtaa) ret.ForeignDtaaApplies = dtaa;

        // Touching the working draft moves it out of the pristine Draft state.
        if (ret.Status == ReturnStatus.Draft)
        {
            ret.Status = ReturnStatus.InProgress;
        }

        await _db.SaveChangesAsync(ct);
        return await BuildDetailAsync(ret.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);

        if (ret.Status is ReturnStatus.Filed or ReturnStatus.Processed)
        {
            throw AppException.Conflict("A filed return cannot be deleted.", "RETURN.ALREADY_FILED");
        }

        ret.DeletedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // =========================================================== income sources

    public async Task<IReadOnlyList<IncomeSourceDto>> ListIncomeSourcesAsync(Guid id, CancellationToken ct = default)
    {
        await LoadOwnedReturnAsync(id, ct);
        return await _db.IncomeSources
            .Where(s => s.TaxReturnId == id)
            .OrderBy(s => s.Type)
            .Select(s => new IncomeSourceDto(s.Id, s.Type, s.Label, s.Amount, s.SourceMetaJson))
            .ToListAsync(ct);
    }

    public async Task<IncomeSourceDto> AddIncomeSourceAsync(Guid id, UpsertIncomeSourceRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = new IncomeSource
        {
            TenantId = ret.TenantId,
            TaxReturnId = ret.Id,
            Type = request.Type,
            Label = request.Label?.Trim(),
            Amount = request.Amount,
            SourceMetaJson = NormalizeJson(request.SourceMetaJson, "RETURN.META_INVALID")
        };

        _db.IncomeSources.Add(entity);
        await MarkInProgressAndSaveAsync(ret, ct);

        return new IncomeSourceDto(entity.Id, entity.Type, entity.Label, entity.Amount, entity.SourceMetaJson);
    }

    public async Task<IncomeSourceDto> UpdateIncomeSourceAsync(Guid id, Guid sourceId, UpsertIncomeSourceRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = await _db.IncomeSources.FirstOrDefaultAsync(s => s.Id == sourceId && s.TaxReturnId == id, ct)
                     ?? throw AppException.NotFound("Income source not found.", "RETURN.SOURCE_NOT_FOUND");

        entity.Type = request.Type;
        entity.Label = request.Label?.Trim();
        entity.Amount = request.Amount;
        entity.SourceMetaJson = NormalizeJson(request.SourceMetaJson, "RETURN.META_INVALID");

        await MarkInProgressAndSaveAsync(ret, ct);
        return new IncomeSourceDto(entity.Id, entity.Type, entity.Label, entity.Amount, entity.SourceMetaJson);
    }

    public async Task DeleteIncomeSourceAsync(Guid id, Guid sourceId, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = await _db.IncomeSources.FirstOrDefaultAsync(s => s.Id == sourceId && s.TaxReturnId == id, ct)
                     ?? throw AppException.NotFound("Income source not found.", "RETURN.SOURCE_NOT_FOUND");

        entity.DeletedAt = _clock.UtcNow;
        await MarkInProgressAndSaveAsync(ret, ct);
    }

    // =========================================================== salary

    public async Task<IReadOnlyList<SalaryDetailDto>> ListSalariesAsync(Guid id, CancellationToken ct = default)
    {
        await LoadOwnedReturnAsync(id, ct);
        var entities = await _db.SalaryDetails
            .Where(s => s.TaxReturnId == id)
            .Include(s => s.Components)
            .OrderBy(s => s.Employer)
            .ToListAsync(ct);
        return entities.Select(ToSalaryDto).ToList();
    }

    public async Task<SalaryDetailDto> AddSalaryAsync(Guid id, UpsertSalaryRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        if (string.IsNullOrWhiteSpace(request.Employer))
        {
            throw AppException.Validation("Employer name is required.", "RETURN.SALARY_EMPLOYER_REQUIRED");
        }

        var entity = new SalaryDetail
        {
            TenantId = ret.TenantId,
            TaxReturnId = ret.Id,
            Employer = request.Employer.Trim(),
            Tan = request.Tan?.Trim(),
            Gross = request.Gross,
            Hra = request.Hra,
            Perquisites = request.Perquisites,
            ProfitsInLieu = request.ProfitsInLieu,
            ExemptAllowances = request.ExemptAllowances,
            HraExemption = request.HraExemption,
            StdDeduction = request.StdDeduction,
            ProfessionalTax = request.ProfessionalTax
        };

        ApplySalaryBreakup(entity, request);
        _db.SalaryDetails.Add(entity);
        await MarkInProgressAndSaveAsync(ret, ct);
        return ToSalaryDto(entity);
    }

    public async Task<SalaryDetailDto> UpdateSalaryAsync(Guid id, Guid salaryId, UpsertSalaryRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = await _db.SalaryDetails
                         .Include(s => s.Components)
                         .FirstOrDefaultAsync(s => s.Id == salaryId && s.TaxReturnId == id, ct)
                     ?? throw AppException.NotFound("Salary detail not found.", "RETURN.SALARY_NOT_FOUND");

        if (string.IsNullOrWhiteSpace(request.Employer))
        {
            throw AppException.Validation("Employer name is required.", "RETURN.SALARY_EMPLOYER_REQUIRED");
        }

        entity.Employer = request.Employer.Trim();
        entity.Tan = request.Tan?.Trim();
        entity.Gross = request.Gross;
        entity.Hra = request.Hra;
        entity.Perquisites = request.Perquisites;
        entity.ProfitsInLieu = request.ProfitsInLieu;
        entity.ExemptAllowances = request.ExemptAllowances;
        entity.HraExemption = request.HraExemption;
        entity.StdDeduction = request.StdDeduction;
        entity.ProfessionalTax = request.ProfessionalTax;

        ApplySalaryBreakup(entity, request);
        await MarkInProgressAndSaveAsync(ret, ct);
        return ToSalaryDto(entity);
    }

    public async Task DeleteSalaryAsync(Guid id, Guid salaryId, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = await _db.SalaryDetails.FirstOrDefaultAsync(s => s.Id == salaryId && s.TaxReturnId == id, ct)
                     ?? throw AppException.NotFound("Salary detail not found.", "RETURN.SALARY_NOT_FOUND");

        _db.SalaryDetails.Remove(entity); // SalaryDetail is not soft-deletable.
        await MarkInProgressAndSaveAsync(ret, ct);
    }

    /// <summary>
    /// When an itemised Schedule S breakup is supplied, (re)build the salary's component rows and
    /// roll them up into the flat SalaryDetail fields the engine consumes. No breakup ⇒ the flat
    /// fields already set from the request are used as-is (backward compatible).
    /// </summary>
    private static void ApplySalaryBreakup(SalaryDetail entity, UpsertSalaryRequest request)
    {
        // Null OR empty breakup ⇒ keep the flat fields from the request as-is. The flat-entry UI path
        // always serialises components as [], which must NOT roll up to zero and wipe the salary.
        if (request.Components is null || request.Components.Count == 0)
        {
            return;
        }

        entity.Components.Clear();
        foreach (var c in request.Components)
        {
            entity.Components.Add(new SalaryComponent
            {
                TenantId = entity.TenantId,
                Label = (c.Label ?? string.Empty).Trim(),
                Category = c.Category,
                Total = c.Total < 0m ? 0m : c.Total,
                Exempt = c.Exempt < 0m ? 0m : c.Exempt,
                IsHra = c.IsHra,
            });
        }

        SalaryRollup.Apply(entity, entity.Components);
    }

    // =========================================================== house property

    public async Task<IReadOnlyList<HousePropertyDto>> ListHousePropertiesAsync(Guid id, CancellationToken ct = default)
    {
        await LoadOwnedReturnAsync(id, ct);
        return await _db.HouseProperties
            .Where(h => h.TaxReturnId == id)
            .Select(h => ToHouseDto(h))
            .ToListAsync(ct);
    }

    public async Task<HousePropertyDto> AddHousePropertyAsync(Guid id, UpsertHousePropertyRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = new HouseProperty
        {
            TenantId = ret.TenantId,
            TaxReturnId = ret.Id,
            Type = request.Type,
            Address = request.Address?.Trim(),
            AnnualValue = request.AnnualValue,
            AnnualRent = request.AnnualRent,
            MunicipalTaxPaid = request.MunicipalTaxPaid,
            InterestOnLoan = request.InterestOnLoan,
            CoOwnerSharePct = NormalizeSharePct(request.CoOwnerSharePct)
        };

        ApplyHousePropertyDerived(entity);
        _db.HouseProperties.Add(entity);
        await MarkInProgressAndSaveAsync(ret, ct);
        return ToHouseDto(entity);
    }

    public async Task<HousePropertyDto> UpdateHousePropertyAsync(Guid id, Guid propertyId, UpsertHousePropertyRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = await _db.HouseProperties.FirstOrDefaultAsync(h => h.Id == propertyId && h.TaxReturnId == id, ct)
                     ?? throw AppException.NotFound("House property not found.", "RETURN.HOUSE_NOT_FOUND");

        entity.Type = request.Type;
        entity.Address = request.Address?.Trim();
        entity.AnnualValue = request.AnnualValue;
        entity.AnnualRent = request.AnnualRent;
        entity.MunicipalTaxPaid = request.MunicipalTaxPaid;
        entity.InterestOnLoan = request.InterestOnLoan;
        entity.CoOwnerSharePct = NormalizeSharePct(request.CoOwnerSharePct);

        ApplyHousePropertyDerived(entity);
        await MarkInProgressAndSaveAsync(ret, ct);
        return ToHouseDto(entity);
    }

    public async Task DeleteHousePropertyAsync(Guid id, Guid propertyId, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = await _db.HouseProperties.FirstOrDefaultAsync(h => h.Id == propertyId && h.TaxReturnId == id, ct)
                     ?? throw AppException.NotFound("House property not found.", "RETURN.HOUSE_NOT_FOUND");

        _db.HouseProperties.Remove(entity); // Not soft-deletable.
        await MarkInProgressAndSaveAsync(ret, ct);
    }

    // =========================================================== capital gains

    public async Task<IReadOnlyList<CapitalGainDto>> ListCapitalGainsAsync(Guid id, CancellationToken ct = default)
    {
        await LoadOwnedReturnAsync(id, ct);
        return await _db.CapitalGains
            .Where(c => c.TaxReturnId == id)
            .Select(c => ToCapitalGainDto(c))
            .ToListAsync(ct);
    }

    public async Task<CapitalGainDto> AddCapitalGainAsync(Guid id, UpsertCapitalGainRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = new CapitalGain
        {
            TenantId = ret.TenantId,
            TaxReturnId = ret.Id,
            AssetType = request.AssetType,
            Term = request.Term,
            TaxSection = request.TaxSection?.Trim(),
            AcquisitionDate = request.AcquisitionDate,
            TransferDate = request.TransferDate,
            SalePrice = request.SalePrice,
            CostOfAcquisition = request.CostOfAcquisition,
            CostOfImprovement = request.CostOfImprovement,
            ExpensesOnTransfer = request.ExpensesOnTransfer,
            ExemptionSection = request.ExemptionSection?.Trim(),
            ExemptionAmount = request.ExemptionAmount,
            ReinvestmentAmount = request.ReinvestmentAmount,
            Isin = request.Isin?.Trim()
        };

        ApplyCapitalGainDerived(entity);
        _db.CapitalGains.Add(entity);
        await MarkInProgressAndSaveAsync(ret, ct);
        return ToCapitalGainDto(entity);
    }

    public async Task<CapitalGainDto> UpdateCapitalGainAsync(Guid id, Guid gainId, UpsertCapitalGainRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = await _db.CapitalGains.FirstOrDefaultAsync(c => c.Id == gainId && c.TaxReturnId == id, ct)
                     ?? throw AppException.NotFound("Capital gain not found.", "RETURN.CG_NOT_FOUND");

        entity.AssetType = request.AssetType;
        entity.Term = request.Term;
        entity.TaxSection = request.TaxSection?.Trim();
        entity.AcquisitionDate = request.AcquisitionDate;
        entity.TransferDate = request.TransferDate;
        entity.SalePrice = request.SalePrice;
        entity.CostOfAcquisition = request.CostOfAcquisition;
        entity.CostOfImprovement = request.CostOfImprovement;
        entity.ExpensesOnTransfer = request.ExpensesOnTransfer;
        entity.ExemptionSection = request.ExemptionSection?.Trim();
        entity.ExemptionAmount = request.ExemptionAmount;
        entity.ReinvestmentAmount = request.ReinvestmentAmount;
        entity.Isin = request.Isin?.Trim();

        ApplyCapitalGainDerived(entity);
        await MarkInProgressAndSaveAsync(ret, ct);
        return ToCapitalGainDto(entity);
    }

    public async Task DeleteCapitalGainAsync(Guid id, Guid gainId, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = await _db.CapitalGains.FirstOrDefaultAsync(c => c.Id == gainId && c.TaxReturnId == id, ct)
                     ?? throw AppException.NotFound("Capital gain not found.", "RETURN.CG_NOT_FOUND");

        entity.DeletedAt = _clock.UtcNow;
        await MarkInProgressAndSaveAsync(ret, ct);
    }

    // =========================================================== business income

    public async Task<IReadOnlyList<BusinessIncomeDto>> ListBusinessIncomesAsync(Guid id, CancellationToken ct = default)
    {
        await LoadOwnedReturnAsync(id, ct);
        return await _db.BusinessIncomes
            .Where(b => b.TaxReturnId == id)
            .Select(b => ToBusinessDto(b))
            .ToListAsync(ct);
    }

    public async Task<BusinessIncomeDto> AddBusinessIncomeAsync(Guid id, UpsertBusinessIncomeRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = new BusinessIncome
        {
            TenantId = ret.TenantId,
            TaxReturnId = ret.Id,
            NatureOfBusinessCode = request.NatureOfBusinessCode?.Trim(),
            AccountingMethod = string.IsNullOrWhiteSpace(request.AccountingMethod) ? "mercantile" : request.AccountingMethod.Trim(),
            IsPresumptive = request.IsPresumptive,
            PresumptiveSection = request.PresumptiveSection?.Trim(),
            Turnover = request.Turnover,
            GrossReceiptsDigital = request.GrossReceiptsDigital,
            GrossReceiptsCash = request.GrossReceiptsCash,
            NetProfit = request.NetProfit,
            SpeculativeFlag = request.SpeculativeFlag,
            GstTurnoverReported = request.GstTurnoverReported
        };

        ApplyBusinessIncomeDerived(entity);
        _db.BusinessIncomes.Add(entity);
        await MarkInProgressAndSaveAsync(ret, ct);
        return ToBusinessDto(entity);
    }

    public async Task<BusinessIncomeDto> UpdateBusinessIncomeAsync(Guid id, Guid businessId, UpsertBusinessIncomeRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = await _db.BusinessIncomes.FirstOrDefaultAsync(b => b.Id == businessId && b.TaxReturnId == id, ct)
                     ?? throw AppException.NotFound("Business income not found.", "RETURN.BIZ_NOT_FOUND");

        entity.NatureOfBusinessCode = request.NatureOfBusinessCode?.Trim();
        entity.AccountingMethod = string.IsNullOrWhiteSpace(request.AccountingMethod) ? "mercantile" : request.AccountingMethod.Trim();
        entity.IsPresumptive = request.IsPresumptive;
        entity.PresumptiveSection = request.PresumptiveSection?.Trim();
        entity.Turnover = request.Turnover;
        entity.GrossReceiptsDigital = request.GrossReceiptsDigital;
        entity.GrossReceiptsCash = request.GrossReceiptsCash;
        entity.NetProfit = request.NetProfit;
        entity.SpeculativeFlag = request.SpeculativeFlag;
        entity.GstTurnoverReported = request.GstTurnoverReported;

        ApplyBusinessIncomeDerived(entity);
        await MarkInProgressAndSaveAsync(ret, ct);
        return ToBusinessDto(entity);
    }

    public async Task DeleteBusinessIncomeAsync(Guid id, Guid businessId, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = await _db.BusinessIncomes.FirstOrDefaultAsync(b => b.Id == businessId && b.TaxReturnId == id, ct)
                     ?? throw AppException.NotFound("Business income not found.", "RETURN.BIZ_NOT_FOUND");

        _db.BusinessIncomes.Remove(entity); // Not soft-deletable.
        await MarkInProgressAndSaveAsync(ret, ct);
    }

    // =========================================================== deductions

    public async Task<IReadOnlyList<DeductionDto>> ListDeductionsAsync(Guid id, CancellationToken ct = default)
    {
        await LoadOwnedReturnAsync(id, ct);
        return await _db.Deductions
            .Where(d => d.TaxReturnId == id)
            .OrderBy(d => d.Section)
            .Select(d => new DeductionDto(d.Id, d.Section, d.SubType, d.Description, d.Amount, d.EligibleAmount, d.RegimeApplicable))
            .ToListAsync(ct);
    }

    public async Task<DeductionDto> AddDeductionAsync(Guid id, UpsertDeductionRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        if (string.IsNullOrWhiteSpace(request.Section))
        {
            throw AppException.Validation("Deduction section is required.", "RETURN.DEDUCTION_SECTION_REQUIRED");
        }

        if (request.Amount < 0)
        {
            throw AppException.Validation("Deduction amount cannot be negative.", "RETURN.DEDUCTION_NEGATIVE");
        }

        var section = request.Section.Trim();
        var entity = new Deduction
        {
            TenantId = ret.TenantId,
            TaxReturnId = ret.Id,
            Section = section,
            SubType = request.SubType?.Trim(),
            Description = request.Description?.Trim(),
            Amount = request.Amount,
            EligibleAmount = CapEligible(section, request.Amount),
            RegimeApplicable = request.RegimeApplicable
        };

        _db.Deductions.Add(entity);
        await MarkInProgressAndSaveAsync(ret, ct);
        return new DeductionDto(entity.Id, entity.Section, entity.SubType, entity.Description, entity.Amount, entity.EligibleAmount, entity.RegimeApplicable);
    }

    public async Task<DeductionDto> UpdateDeductionAsync(Guid id, Guid deductionId, UpsertDeductionRequest request, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = await _db.Deductions.FirstOrDefaultAsync(d => d.Id == deductionId && d.TaxReturnId == id, ct)
                     ?? throw AppException.NotFound("Deduction not found.", "RETURN.DEDUCTION_NOT_FOUND");

        if (string.IsNullOrWhiteSpace(request.Section))
        {
            throw AppException.Validation("Deduction section is required.", "RETURN.DEDUCTION_SECTION_REQUIRED");
        }

        if (request.Amount < 0)
        {
            throw AppException.Validation("Deduction amount cannot be negative.", "RETURN.DEDUCTION_NEGATIVE");
        }

        entity.Section = request.Section.Trim();
        entity.SubType = request.SubType?.Trim();
        entity.Description = request.Description?.Trim();
        entity.Amount = request.Amount;
        entity.EligibleAmount = CapEligible(entity.Section, request.Amount);
        entity.RegimeApplicable = request.RegimeApplicable;

        await MarkInProgressAndSaveAsync(ret, ct);
        return new DeductionDto(entity.Id, entity.Section, entity.SubType, entity.Description, entity.Amount, entity.EligibleAmount, entity.RegimeApplicable);
    }

    public async Task DeleteDeductionAsync(Guid id, Guid deductionId, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        EnsureMutable(ret);

        var entity = await _db.Deductions.FirstOrDefaultAsync(d => d.Id == deductionId && d.TaxReturnId == id, ct)
                     ?? throw AppException.NotFound("Deduction not found.", "RETURN.DEDUCTION_NOT_FOUND");

        entity.DeletedAt = _clock.UtcNow;
        await MarkInProgressAndSaveAsync(ret, ct);
    }

    // =========================================================== validate

    public async Task<ValidateReturnResponse> ValidateAsync(Guid id, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);
        var findings = new List<ValidationFinding>();

        var salaries = await _db.SalaryDetails.Where(s => s.TaxReturnId == id).ToListAsync(ct);
        var houses = await _db.HouseProperties.Where(h => h.TaxReturnId == id).ToListAsync(ct);
        var gains = await _db.CapitalGains.Where(c => c.TaxReturnId == id).ToListAsync(ct);
        var businesses = await _db.BusinessIncomes.Where(b => b.TaxReturnId == id).ToListAsync(ct);
        var incomeSources = await _db.IncomeSources.Where(s => s.TaxReturnId == id).ToListAsync(ct);
        var deductions = await _db.Deductions.Where(d => d.TaxReturnId == id).ToListAsync(ct);

        var hasAnyIncome = salaries.Count > 0 || houses.Count > 0 || gains.Count > 0
                           || businesses.Count > 0 || incomeSources.Count > 0;

        // --- completeness: an ITR type and at least one income head are required to file. ---
        if (ret.ItrType is null)
        {
            findings.Add(new ValidationFinding("block", "RETURN.ITR_TYPE_MISSING",
                "Select or auto-suggest an ITR type before filing.", "itrType"));
        }

        if (!hasAnyIncome)
        {
            findings.Add(new ValidationFinding("block", "RETURN.NO_INCOME",
                "Add at least one income source before filing.", null));
        }

        // --- per-head sanity checks ---
        foreach (var s in salaries.Where(s => s.Gross <= 0))
        {
            findings.Add(new ValidationFinding("warn", "RETURN.SALARY_ZERO_GROSS",
                $"Salary from '{s.Employer}' has zero gross — confirm the Form 16 figures.", "salaries"));
        }

        foreach (var c in gains.Where(c => c.SalePrice <= 0))
        {
            findings.Add(new ValidationFinding("warn", "RETURN.CG_ZERO_SALE",
                "A capital-gain entry has no sale consideration.", "capitalGains"));
        }

        // --- deduction caps (illustrative; authoritative capping is in the engine). ---
        foreach (var group in deductions.GroupBy(d => d.Section, StringComparer.OrdinalIgnoreCase))
        {
            if (!SectionCaps.TryGetValue(group.Key, out var cap))
            {
                continue;
            }

            var claimed = group.Sum(d => d.Amount);
            if (claimed > cap)
            {
                findings.Add(new ValidationFinding("warn", "TAX.DEDUCTION_LIMIT_EXCEEDED",
                    $"{group.Key} claims {claimed:0.##} which exceeds the {cap:0.##} ceiling; only the cap will be allowed.",
                    $"deductions.{group.Key}"));
            }
        }

        // --- ITR-type consistency against the auto-selector (docs 03 §3.2). ---
        if (ret.ItrType is { } chosen)
        {
            var verdict = await _selector.SuggestForReturnAsync(id, ct);
            if (verdict.RecommendedForm > chosen)
            {
                // Chosen a simpler form than the income profile supports — that is a hard block.
                findings.Add(new ValidationFinding("block", "RETURN.ITR_TYPE_MISMATCH",
                    $"Income profile requires {FormName(verdict.RecommendedForm)} but {FormName(chosen)} is selected ({string.Join(", ", verdict.DecidingFlags)}).",
                    "itrType"));
            }
            else if (verdict.RecommendedForm != chosen)
            {
                findings.Add(new ValidationFinding("info", "RETURN.ITR_TYPE_SUGGESTION",
                    $"A simpler form ({FormName(verdict.RecommendedForm)}) may also be eligible.", "itrType"));
            }
        }

        var canFile = findings.All(f => f.Severity != "block");

        // Validation that passes promotes a still-draft return to computed-ready so the
        // pay → file saga can proceed (does not regress a return already further along).
        if (canFile && ret.Status is ReturnStatus.Draft or ReturnStatus.InProgress)
        {
            ret.Status = ReturnStatus.ComputedReady;
            await _db.SaveChangesAsync(ct);
        }

        return new ValidateReturnResponse(canFile, findings);
    }

    // =========================================================== submit

    public async Task<SubmitReturnResponse> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);

        // Idempotent: a return already filed returns its existing acknowledgment (docs 04 §4.1).
        if (ret.Status is ReturnStatus.Filed or ReturnStatus.Processed)
        {
            var latest = await _db.ReturnVersions
                .Where(v => v.TaxReturnId == id)
                .OrderByDescending(v => v.VersionNo)
                .FirstOrDefaultAsync(ct);

            return new SubmitReturnResponse(
                ret.Id, ret.Status, ret.AcknowledgmentNumber ?? string.Empty,
                ret.SubmittedAt ?? ret.UpdatedAt, latest?.VersionNo ?? 0, latest?.JsonHash ?? string.Empty);
        }

        // Payment gate: filing requires a Paid (or CA-cleared) return (docs 04 error PAYMENT.REQUIRED).
        if (ret.Status is not (ReturnStatus.Paid or ReturnStatus.ReadyToFile))
        {
            throw new AppException("PAYMENT.REQUIRED",
                "The filing fee must be paid before the return can be submitted.", 402);
        }

        if (ret.ItrType is null)
        {
            throw AppException.Validation("Select an ITR type before filing.", "RETURN.ITR_TYPE_MISSING");
        }

        var ay = await _db.AssessmentYears.FirstOrDefaultAsync(a => a.Id == ret.AssessmentYearId, ct)
                 ?? throw new AppException("RETURN.AY_MISSING", "Assessment year is missing for this return.", 500);

        // Build the canonical snapshot + hash BEFORE calling ERI so the payload we file is the
        // exact payload we persist (reproducibility, docs 03 §3.11).
        var snapshot = await BuildSnapshotAsync(ret, ay.Code, ct);
        var snapshotJson = JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
        var hash = Sha256Hex(snapshotJson);

        EFilingResult result;
        try
        {
            result = await _eFiling.SubmitAsync(ret.Id, ay.Code, snapshotJson, ct);
        }
        catch (Exception ex)
        {
            ret.Status = ReturnStatus.Failed;
            await _db.SaveChangesAsync(ct);
            _logger.LogError(ex, "E-filing failed for return {ReturnId}", ret.Id);
            throw new AppException("INTEGRATION.ITD_UNAVAILABLE", "The e-filing service is currently unavailable.", 502, ex);
        }

        if (!result.Accepted)
        {
            ret.Status = ReturnStatus.Failed;
            await _db.SaveChangesAsync(ct);
            throw new AppException("INTEGRATION.ITD_REJECTED",
                result.FailureReason ?? "The return was rejected by the e-filing service.", 502);
        }

        var now = _clock.UtcNow;
        ret.Status = ReturnStatus.Filed;
        ret.AcknowledgmentNumber = result.AcknowledgmentNumber;
        ret.SubmittedAt = result.SubmittedAt;

        // Append-only pre-file snapshot version (docs 02 §2.5).
        var versionNo = await NextVersionNoAsync(id, ct);
        var version = new ReturnVersion
        {
            TenantId = ret.TenantId,
            TaxReturnId = ret.Id,
            VersionNo = versionNo,
            Reason = "pre_file",
            RuleSetVersion = ret.RuleSetVersion,
            SnapshotJson = snapshotJson,
            JsonHash = hash,
            CreatedByUserId = _currentUser.UserId
        };
        _db.ReturnVersions.Add(version);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Filed return {ReturnId} ack={Ack} version={Version}", ret.Id, result.AcknowledgmentNumber, versionNo);

        return new SubmitReturnResponse(ret.Id, ret.Status, result.AcknowledgmentNumber, now, versionNo, hash);
    }

    // =========================================================== status / suggest

    public async Task<ReturnStatusDto> GetStatusAsync(Guid id, CancellationToken ct = default)
    {
        var ret = await LoadOwnedReturnAsync(id, ct);

        // For a filed return, reconcile against the (stubbed) ERI processing status.
        if (ret.Status == ReturnStatus.Filed && !string.IsNullOrEmpty(ret.AcknowledgmentNumber))
        {
            var remote = await _eFiling.GetStatusAsync(ret.AcknowledgmentNumber, ct);
            if (remote.Accepted)
            {
                ret.Status = ReturnStatus.Processed;
                await _db.SaveChangesAsync(ct);
            }
        }

        return new ReturnStatusDto(ret.Id, ret.Status, ret.AcknowledgmentNumber, ret.SubmittedAt, ret.EVerifiedAt);
    }

    public async Task<ItrSelectionVerdict> SuggestTypeAsync(Guid id, CancellationToken ct = default)
    {
        await LoadOwnedReturnAsync(id, ct);
        return await _selector.SuggestForReturnAsync(id, ct);
    }

    // =========================================================== internals

    /// <summary>
    /// Load a return owned by the current user within the current tenant, or throw 404.
    /// This is the single ownership gate every operation routes through (docs 04 §4.5).
    /// </summary>
    private async Task<TaxReturn> LoadOwnedReturnAsync(Guid id, CancellationToken ct)
    {
        return await _db.TaxReturns
                   .FirstOrDefaultAsync(r => r.Id == id
                                             && r.TenantId == _currentUser.TenantId
                                             && r.UserId == _currentUser.UserId, ct)
               ?? throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");
    }

    /// <summary>A return that has been filed (or is processed) is frozen — edits are 409.</summary>
    private static void EnsureMutable(TaxReturn ret)
    {
        if (ret.Status is ReturnStatus.Filed or ReturnStatus.Processed)
        {
            throw AppException.Conflict("A filed return can no longer be edited.", "RETURN.ALREADY_FILED");
        }
    }

    private async Task MarkInProgressAndSaveAsync(TaxReturn ret, CancellationToken ct)
    {
        // Any change to income/deduction data invalidates a prior "computed" state.
        if (ret.Status is ReturnStatus.Draft or ReturnStatus.ComputedReady)
        {
            ret.Status = ReturnStatus.InProgress;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<ReturnDetailDto> BuildDetailAsync(Guid id, CancellationToken ct)
    {
        var ret = await _db.TaxReturns
                      .Include(r => r.AssessmentYear)
                      .FirstOrDefaultAsync(r => r.Id == id
                                                && r.TenantId == _currentUser.TenantId
                                                && r.UserId == _currentUser.UserId, ct)
                  ?? throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");

        var incomeSources = await _db.IncomeSources.Where(s => s.TaxReturnId == id)
            .Select(s => new IncomeSourceDto(s.Id, s.Type, s.Label, s.Amount, s.SourceMetaJson)).ToListAsync(ct);
        var salaryEntities = await _db.SalaryDetails.Where(s => s.TaxReturnId == id).Include(s => s.Components).ToListAsync(ct);
        var salaries = salaryEntities.Select(ToSalaryDto).ToList();
        var houses = await _db.HouseProperties.Where(h => h.TaxReturnId == id).Select(h => ToHouseDto(h)).ToListAsync(ct);
        var gains = await _db.CapitalGains.Where(c => c.TaxReturnId == id).Select(c => ToCapitalGainDto(c)).ToListAsync(ct);
        var businesses = await _db.BusinessIncomes.Where(b => b.TaxReturnId == id).Select(b => ToBusinessDto(b)).ToListAsync(ct);
        var deductions = await _db.Deductions.Where(d => d.TaxReturnId == id)
            .Select(d => new DeductionDto(d.Id, d.Section, d.SubType, d.Description, d.Amount, d.EligibleAmount, d.RegimeApplicable)).ToListAsync(ct);

        // Materialize the return's (at most two) computations before ordering: SQLite cannot
        // ORDER BY the DateTimeOffset ComputedAt column. Prefer the recommended, then the newest.
        var computation = (await _db.TaxComputations
                .Where(c => c.TaxReturnId == id)
                .ToListAsync(ct))
            .OrderByDescending(c => c.IsRecommended).ThenByDescending(c => c.ComputedAt)
            .Select(ToComputationDto)
            .FirstOrDefault();

        return new ReturnDetailDto(
            ret.Id,
            ret.AssessmentYear?.Code ?? string.Empty,
            ret.ItrType,
            ret.Status,
            ret.Regime,
            ret.RuleSetVersion,
            ret.QuestionnaireSchemaVersion,
            ret.AnswersJson,
            ret.FilingMode,
            ret.IsRevised,
            ret.AcknowledgmentNumber,
            ret.CreatedAt,
            ret.SubmittedAt,
            ret.EVerifiedAt,
            incomeSources,
            salaries,
            houses,
            gains,
            businesses,
            deductions,
            computation,
            ret.TdsPaid,
            ret.TcsPaid,
            ret.AdvanceTaxPaid,
            ret.SelfAssessmentTaxPaid,
            ret.BroughtForwardHousePropertyLoss,
            ret.BroughtForwardBusinessLoss,
            ret.BroughtForwardShortTermCapitalLoss,
            ret.BroughtForwardLongTermCapitalLoss,
            ret.BroughtForwardAmtCredit,
            ret.Relief89,
            ret.ForeignIncomeDoublyTaxed,
            ret.ForeignTaxPaid,
            ret.ForeignDtaaApplies);
    }

    /// <summary>
    /// Build the canonical, regime-agnostic snapshot persisted into <see cref="ReturnVersion"/> and
    /// sent to the e-filing stub. Includes the latest persisted computation so the filed payload is
    /// self-contained and reproducible (docs 03 §3.11). Optionally re-computes via the engine if it
    /// is implemented; if not, falls back to whatever is already persisted.
    /// </summary>
    private async Task<object> BuildSnapshotAsync(TaxReturn ret, string ayCode, CancellationToken ct)
    {
        var salaries = await _db.SalaryDetails.Where(s => s.TaxReturnId == ret.Id).ToListAsync(ct);
        var houses = await _db.HouseProperties.Where(h => h.TaxReturnId == ret.Id).ToListAsync(ct);
        var gains = await _db.CapitalGains.Where(c => c.TaxReturnId == ret.Id).ToListAsync(ct);
        var businesses = await _db.BusinessIncomes.Where(b => b.TaxReturnId == ret.Id).ToListAsync(ct);
        var incomeSources = await _db.IncomeSources.Where(s => s.TaxReturnId == ret.Id).ToListAsync(ct);
        var deductions = await _db.Deductions.Where(d => d.TaxReturnId == ret.Id).ToListAsync(ct);

        // Ensure a computation exists for the chosen regime; persist one if the engine can produce it.
        var computation = await EnsureComputationAsync(ret, ayCode, salaries, houses, gains, businesses, incomeSources, deductions, ct);

        return new
        {
            ret.Id,
            assessmentYear = ayCode,
            itrType = ret.ItrType?.ToString(),
            regime = ret.Regime?.ToString(),
            ruleSetVersion = ret.RuleSetVersion,
            questionnaireSchemaVersion = ret.QuestionnaireSchemaVersion,
            filingMode = ret.FilingMode,
            salaries = salaries.Select(s => new { s.Employer, s.Tan, s.Gross, s.Perquisites, s.ExemptAllowances, s.HraExemption, s.StdDeduction, s.ProfessionalTax }),
            houseProperties = houses.Select(h => new { type = h.Type.ToString(), h.AnnualValue, h.MunicipalTaxPaid, h.StdDeduction30Pct, h.InterestOnLoan, h.CoOwnerSharePct, h.NetIncome }),
            capitalGains = gains.Select(c => new { assetType = c.AssetType.ToString(), term = c.Term.ToString(), c.TaxSection, c.SalePrice, c.CostOfAcquisition, c.IndexedCost, c.ExemptionAmount, c.Gain }),
            businessIncomes = businesses.Select(b => new { b.IsPresumptive, b.PresumptiveSection, b.Turnover, b.GrossReceiptsDigital, b.GrossReceiptsCash, b.PresumptiveRatePct, b.NetProfit, b.SpeculativeFlag }),
            otherIncomeSources = incomeSources.Select(s => new { type = s.Type.ToString(), s.Label, s.Amount }),
            deductions = deductions.Select(d => new { d.Section, d.SubType, d.Amount, d.EligibleAmount, regime = d.RegimeApplicable?.ToString() }),
            computation = computation is null ? null : new
            {
                regime = computation.Regime.ToString(),
                computation.GrossTotalIncome,
                computation.TotalDeductions,
                computation.TaxableIncome,
                computation.TotalTax,
                computation.RefundOrPayable
            }
        };
    }

    /// <summary>
    /// Returns the persisted computation for the chosen regime, computing + persisting one first if
    /// the tax engine is available. The engine is implemented by a parallel module; if it still
    /// throws <see cref="NotImplementedException"/> we degrade gracefully and rely on any
    /// computation the Tax module persisted, so the Returns flow never hard-fails on filing.
    /// </summary>
    private async Task<TaxComputation?> EnsureComputationAsync(
        TaxReturn ret,
        string ayCode,
        IReadOnlyList<SalaryDetail> salaries,
        IReadOnlyList<HouseProperty> houses,
        IReadOnlyList<CapitalGain> gains,
        IReadOnlyList<BusinessIncome> businesses,
        IReadOnlyList<IncomeSource> incomeSources,
        IReadOnlyList<Deduction> deductions,
        CancellationToken ct)
    {
        var regime = ret.Regime ?? Regime.New;

        // Materialize before ordering (SQLite cannot ORDER BY the DateTimeOffset ComputedAt).
        var existing = (await _db.TaxComputations
                .Where(c => c.TaxReturnId == ret.Id && c.Regime == regime)
                .ToListAsync(ct))
            .OrderByDescending(c => c.ComputedAt)
            .FirstOrDefault();

        if (existing is not null)
        {
            return existing;
        }

        // No persisted computation — try the engine. Guard against the not-yet-implemented stub.
        var rulesJson = await _db.TaxRuleSets
            .Where(r => r.AssessmentYearId == ret.AssessmentYearId && r.Version == ret.RuleSetVersion)
            .Select(r => r.RulesJson)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(rulesJson))
        {
            return null;
        }

        var input = BuildComputationInput(ret, ayCode, rulesJson, salaries, houses, gains, businesses, incomeSources, deductions);

        ComputationResult result;
        try
        {
            result = _calculator.Compute(input, regime);
        }
        catch (NotImplementedException)
        {
            // Tax engine not yet wired in this build — skip persistence, snapshot omits computation.
            _logger.LogWarning("Tax engine not implemented; filing return {ReturnId} without a fresh computation.", ret.Id);
            return null;
        }

        var computation = new TaxComputation
        {
            TenantId = ret.TenantId,
            TaxReturnId = ret.Id,
            Regime = regime,
            GrossTotalIncome = result.GrossTotalIncome,
            TotalDeductions = result.TotalDeductions,
            TaxableIncome = result.TaxableIncome,
            TaxBeforeCess = result.TaxBeforeRebate - result.Rebate87A + result.Surcharge,
            Cess = result.Cess,
            Rebate87A = result.Rebate87A,
            Surcharge = result.Surcharge,
            TotalTax = result.TotalTax,
            TdsPaid = result.TdsPaid,
            AdvanceTax = result.AdvanceTax,
            InterestPenalty = result.InterestPenalty,
            RefundOrPayable = result.RefundOrPayable,
            IsRecommended = true,
            TraceJson = JsonSerializer.Serialize(result.Trace, SnapshotJsonOptions),
            ComputedAt = _clock.UtcNow
        };

        _db.TaxComputations.Add(computation);
        // Saved together with the return transition by the caller's SaveChanges.
        return computation;
    }

    private TaxComputationInput BuildComputationInput(
        TaxReturn ret,
        string ayCode,
        string rulesJson,
        IReadOnlyList<SalaryDetail> salaries,
        IReadOnlyList<HouseProperty> houses,
        IReadOnlyList<CapitalGain> gains,
        IReadOnlyList<BusinessIncome> businesses,
        IReadOnlyList<IncomeSource> incomeSources,
        IReadOnlyList<Deduction> deductions)
        => TaxComputationInputFactory.FromReturn(
            // Age defaults to an adult slab on this snapshot path (resolved from UserProfile on /tax/compute).
            ret, ayCode, rulesJson, 30, DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
            salaries, houses, gains, businesses, incomeSources, deductions);

    private async Task<int> NextVersionNoAsync(Guid taxReturnId, CancellationToken ct)
    {
        var max = await _db.ReturnVersions
            .Where(v => v.TaxReturnId == taxReturnId)
            .Select(v => (int?)v.VersionNo)
            .MaxAsync(ct);
        return (max ?? 0) + 1;
    }

    // --- derived-field calculators (kept consistent on write, independent of the engine) ---

    /// <summary>Net house-property income: (annual value − municipal tax) − 30% std deduction − loan interest, share-adjusted.</summary>
    private static void ApplyHousePropertyDerived(HouseProperty h)
    {
        var netAnnualValue = Math.Max(0m, h.AnnualValue - h.MunicipalTaxPaid);
        h.StdDeduction30Pct = Math.Round(netAnnualValue * 0.30m, 2, MidpointRounding.AwayFromZero);
        var net = netAnnualValue - h.StdDeduction30Pct - h.InterestOnLoan;

        var share = h.CoOwnerSharePct <= 0 ? 100m : h.CoOwnerSharePct;
        h.NetIncome = Math.Round(net * (share / 100m), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Capital gain = sale − (cost + improvement + transfer expenses) − exemption, floored at 0 only for exemption.</summary>
    private static void ApplyCapitalGainDerived(CapitalGain c)
    {
        // Use indexed cost when supplied (engine may refine it); fall back to actual cost.
        var costBase = c.IndexedCost > 0 ? c.IndexedCost : c.CostOfAcquisition;
        var gross = c.SalePrice - costBase - c.CostOfImprovement - c.ExpensesOnTransfer;
        c.Gain = gross - c.ExemptionAmount;
    }

    /// <summary>
    /// Presumptive deemed profit: 6% on digital + 8% on cash receipts for 44AD; 50% of gross for 44ADA
    /// (docs 03 §3.10). The taxpayer-declared NetProfit wins if higher. Non-presumptive keeps the
    /// supplied NetProfit untouched.
    /// </summary>
    private static void ApplyBusinessIncomeDerived(BusinessIncome b)
    {
        if (!b.IsPresumptive)
        {
            b.PresumptiveRatePct = 0m;
            return;
        }

        var section = (b.PresumptiveSection ?? string.Empty).Trim().ToUpperInvariant();
        decimal deemed;
        switch (section)
        {
            case "44ADA":
                b.PresumptiveRatePct = 50m;
                deemed = Math.Round((b.GrossReceiptsDigital + b.GrossReceiptsCash + b.Turnover) * 0.50m, 2, MidpointRounding.AwayFromZero);
                // When only Turnover is supplied, base 50% on it; otherwise on receipts.
                if (b.GrossReceiptsDigital + b.GrossReceiptsCash > 0)
                {
                    deemed = Math.Round((b.GrossReceiptsDigital + b.GrossReceiptsCash) * 0.50m, 2, MidpointRounding.AwayFromZero);
                }
                break;

            case "44AD":
            default:
                b.PresumptiveRatePct = 8m; // headline rate; digital portion is concessional 6%
                var digital = b.GrossReceiptsDigital;
                var cash = b.GrossReceiptsCash;
                if (digital + cash == 0 && b.Turnover > 0)
                {
                    // No receipt split supplied — treat the whole turnover at the 8% rate.
                    deemed = Math.Round(b.Turnover * 0.08m, 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    deemed = Math.Round(digital * 0.06m + cash * 0.08m, 2, MidpointRounding.AwayFromZero);
                }
                break;
        }

        b.NetProfit = Math.Max(b.NetProfit, deemed);
    }

    private static decimal? CapEligible(string section, decimal amount)
        => SectionCaps.TryGetValue(section, out var cap) ? Math.Min(amount, cap) : amount;

    // --- entity → DTO projections (static so they translate in EF Select where possible) ---

    private static SalaryDetailDto ToSalaryDto(SalaryDetail s) => new(
        s.Id, s.Employer, s.Tan, s.Gross, s.Hra, s.Perquisites, s.ProfitsInLieu,
        s.ExemptAllowances, s.HraExemption, s.StdDeduction, s.ProfessionalTax,
        s.Components
            .OrderBy(c => c.CreatedAt)
            .Select(c => new SalaryComponentDto(c.Id, c.Label, c.Category, c.Total, c.Exempt, c.Total - c.Exempt, c.IsHra))
            .ToList());

    private static HousePropertyDto ToHouseDto(HouseProperty h) => new(
        h.Id, h.Type, h.Address, h.AnnualValue, h.AnnualRent, h.MunicipalTaxPaid,
        h.StdDeduction30Pct, h.InterestOnLoan, h.CoOwnerSharePct, h.NetIncome);

    private static CapitalGainDto ToCapitalGainDto(CapitalGain c) => new(
        c.Id, c.AssetType, c.Term, c.TaxSection, c.AcquisitionDate, c.TransferDate, c.SalePrice,
        c.CostOfAcquisition, c.IndexedCost, c.CostOfImprovement, c.ExpensesOnTransfer,
        c.ExemptionSection, c.ExemptionAmount, c.ReinvestmentAmount, c.Gain, c.Isin);

    private static BusinessIncomeDto ToBusinessDto(BusinessIncome b) => new(
        b.Id, b.NatureOfBusinessCode, b.AccountingMethod, b.IsPresumptive, b.PresumptiveSection,
        b.Turnover, b.GrossReceiptsDigital, b.GrossReceiptsCash, b.PresumptiveRatePct, b.NetProfit,
        b.SpeculativeFlag, b.GstTurnoverReported);

    private static TaxComputationDto ToComputationDto(TaxComputation c) => new(
        c.Id, c.Regime, c.GrossTotalIncome, c.TotalDeductions, c.TaxableIncome, c.TaxBeforeCess,
        c.Cess, c.Rebate87A, c.Surcharge, c.TotalTax, c.TdsPaid, c.AdvanceTax, c.InterestPenalty,
        c.RefundOrPayable, c.IsRecommended, c.ComputedAt);

    // --- misc helpers ---

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        if (page < 1)
        {
            page = 1;
        }

        pageSize = pageSize switch
        {
            <= 0 => 20,
            > 100 => 100, // hard cap per docs 04 §4.1
            _ => pageSize
        };

        return (page, pageSize);
    }

    private static decimal NormalizeSharePct(decimal pct)
        => pct is <= 0 or > 100 ? 100m : pct;

    private static bool TryParseStatus(string? raw, out ReturnStatus status)
    {
        status = default;
        return !string.IsNullOrWhiteSpace(raw) && Enum.TryParse(raw.Trim(), ignoreCase: true, out status);
    }

    private static bool TryParseItrType(string? raw, out ItrType itrType)
    {
        itrType = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        // Accept "ITR1", "ITR-1", "itr1".
        var normalized = raw.Trim().Replace("-", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse(normalized, ignoreCase: true, out itrType);
    }

    private static string FormName(ItrType t) => t switch
    {
        ItrType.ITR1 => "ITR-1",
        ItrType.ITR2 => "ITR-2",
        ItrType.ITR3 => "ITR-3",
        ItrType.ITR4 => "ITR-4",
        _ => t.ToString()
    };

    private static string NormalizeJson(string? raw, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "{}";
        }

        try
        {
            using var _ = JsonDocument.Parse(raw);
            return raw;
        }
        catch (JsonException)
        {
            throw AppException.Validation("The supplied JSON is malformed.", errorCode);
        }
    }

    private static string Sha256Hex(string s)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = false
    };
}
