using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Modules.Accounting;
using TallyG.Tax.Api.Modules.Admin.Audit;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.EReturn;

/// <summary>
/// Orchestrates the offline-filing flow (generate → validate → save-to-list → download). Owner-scoped:
/// a user only ever sees their own returns/artifacts (someone else's is indistinguishable from absent →
/// 404). One latest <see cref="ItrFiling"/> per return; regenerating replaces it. Scrutor binds
/// ItrJsonService : IItrJsonService scoped (no manual DI).
/// </summary>
public sealed class ItrJsonService : IItrJsonService
{
    private static readonly JsonSerializerOptions ReportJsonOpts = new();

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly IItrJsonGenerationService _generator;
    private readonly IItrJsonValidationService _validator;
    private readonly IFinancialStatementsService _financials;
    private readonly IAuditWriterService _audit;
    private readonly ILogger<ItrJsonService> _logger;

    public ItrJsonService(
        AppDbContext db,
        ICurrentUser currentUser,
        IDateTime clock,
        IItrJsonGenerationService generator,
        IItrJsonValidationService validator,
        IFinancialStatementsService financials,
        IAuditWriterService audit,
        ILogger<ItrJsonService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _generator = generator;
        _validator = validator;
        _financials = financials;
        _audit = audit;
        _logger = logger;
    }

    public async Task<GenerateItrJsonResponse> GenerateAsync(Guid returnId, CancellationToken ct = default)
    {
        var ctx = await BuildContextAsync(returnId, ct);
        var generated = _generator.Generate(ctx);                 // may throw ITRJSON.FORM_UNSUPPORTED (422)
        var report = _validator.Validate(ctx, generated.Json);

        var now = _clock.UtcNow;
        var filing = await _db.ItrFilings.FirstOrDefaultAsync(f => f.TaxReturnId == returnId, ct);
        if (filing is null)
        {
            filing = new ItrFiling { TenantId = ctx.Return.TenantId, UserId = ctx.Return.UserId, TaxReturnId = returnId };
            _db.ItrFilings.Add(filing);
        }

        filing.AssessmentYearCode = ctx.AyCode;
        filing.ItrType = ctx.ItrType;
        filing.SchemaVersion = generated.SchemaVersion;
        filing.RawJson = generated.Json;
        filing.JsonHash = Sha256(generated.Json);
        filing.ValidationJson = JsonSerializer.Serialize(report, ReportJsonOpts);
        filing.Status = report.IsValid ? ItrFilingStatus.Valid : ItrFilingStatus.Invalid;
        filing.ErrorCount = report.ErrorCount;
        filing.WarningCount = report.WarningCount;
        filing.GeneratedAt = now;
        filing.ValidatedAt = now;

        _audit.Write("itrjson.generated", "ItrFiling", filing.Id,
            new { returnId, form = generated.FormName, valid = report.IsValid, errors = report.ErrorCount });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Generated ITR JSON for return {ReturnId}: form={Form} valid={Valid} errors={Errors}",
            returnId, generated.FormName, report.IsValid, report.ErrorCount);

        return new GenerateItrJsonResponse(ToDto(filing), report);
    }

    public async Task<ValidationReportDto> ValidateAsync(Guid fileId, CancellationToken ct = default)
    {
        var filing = await LoadOwnedFilingAsync(fileId, ct);
        var ctx = await BuildContextAsync(filing.TaxReturnId, ct);
        var report = _validator.Validate(ctx, filing.RawJson);

        filing.ValidationJson = JsonSerializer.Serialize(report, ReportJsonOpts);
        filing.Status = report.IsValid ? ItrFilingStatus.Valid : ItrFilingStatus.Invalid;
        filing.ErrorCount = report.ErrorCount;
        filing.WarningCount = report.WarningCount;
        filing.ValidatedAt = _clock.UtcNow;

        _audit.Write("itrjson.validated", "ItrFiling", filing.Id, new { valid = report.IsValid, errors = report.ErrorCount });
        await _db.SaveChangesAsync(ct);
        return report;
    }

    public async Task<ValidationReportDto> GetReportAsync(Guid fileId, CancellationToken ct = default)
    {
        var filing = await LoadOwnedFilingAsync(fileId, ct);
        if (!string.IsNullOrWhiteSpace(filing.ValidationJson) && filing.ValidationJson != "{}")
        {
            try
            {
                var report = JsonSerializer.Deserialize<ValidationReportDto>(filing.ValidationJson, ReportJsonOpts);
                if (report is not null)
                {
                    return report;
                }
            }
            catch (JsonException)
            {
                // fall through to the count-only summary below
            }
        }

        return new ValidationReportDto(
            filing.Status == ItrFilingStatus.Valid, filing.ErrorCount, filing.WarningCount,
            Array.Empty<ValidationIssueDto>(), "Not validated yet — run validation to see the details.");
    }

    public async Task<IReadOnlyList<ItrJsonArtifactDto>> ListForReturnAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await _db.ItrFilings
            .Where(f => f.TaxReturnId == returnId)
            .OrderByDescending(f => f.GeneratedAt)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<PagedResult<ItrJsonArtifactDto>> ListMineAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var q = _db.ItrFilings.Where(f => f.TenantId == _currentUser.TenantId && f.UserId == _currentUser.UserId);
        var total = await q.LongCountAsync(ct);
        var rows = await q
            .OrderByDescending(f => f.GeneratedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ItrJsonArtifactDto>(rows.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<ItrJsonArtifactDto> GetAsync(Guid fileId, CancellationToken ct = default)
        => ToDto(await LoadOwnedFilingAsync(fileId, ct));

    public async Task<ItrJsonDownload> DownloadAsync(Guid fileId, CancellationToken ct = default)
    {
        var filing = await LoadOwnedFilingAsync(fileId, ct);
        return new ItrJsonDownload(Encoding.UTF8.GetBytes(filing.RawJson), FileName(filing));
    }

    // ----------------------------------------------------------------- helpers
    private async Task<ItrFilingContext> BuildContextAsync(Guid returnId, CancellationToken ct)
    {
        var ret = await _db.TaxReturns
            .Include(r => r.AssessmentYear)
            .FirstOrDefaultAsync(r => r.Id == returnId
                                      && r.TenantId == _currentUser.TenantId
                                      && r.UserId == _currentUser.UserId, ct)
            ?? throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");

        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == ret.UserId, ct)
            ?? throw AppException.NotFound("User not found.", "USER.NOT_FOUND");
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == ret.UserId, ct);
        var comp = await _db.TaxComputations
            .Where(x => x.TaxReturnId == returnId)
            .OrderByDescending(x => x.IsRecommended)
            .ThenByDescending(x => x.ComputedAt)
            .FirstOrDefaultAsync(ct);

        return new ItrFilingContext
        {
            Return = ret,
            User = user,
            Profile = profile,
            Ay = ret.AssessmentYear,
            Computation = comp,
            GeneratedOn = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
            // ITR-3's Balance Sheet + P&L are derived from the user's books (the accounting ledgers).
            FinancialStatements = ret.ItrType == ItrType.ITR3 ? await _financials.GetAsync(ct) : null,
            Salaries = await _db.SalaryDetails.Where(s => s.TaxReturnId == returnId).ToListAsync(ct),
            Houses = await _db.HouseProperties.Where(h => h.TaxReturnId == returnId).ToListAsync(ct),
            Gains = await _db.CapitalGains.Where(g => g.TaxReturnId == returnId).ToListAsync(ct),
            Businesses = await _db.BusinessIncomes.Where(b => b.TaxReturnId == returnId).ToListAsync(ct),
            OtherIncomes = await _db.IncomeSources
                .Where(s => s.TaxReturnId == returnId && s.Type == IncomeType.OtherSources).ToListAsync(ct),
            Deductions = await _db.Deductions.Where(d => d.TaxReturnId == returnId).ToListAsync(ct),
            BankAccounts = await _db.BankAccountDetails
                .Where(b => b.UserId == ret.UserId && b.TenantId == ret.TenantId).ToListAsync(ct),
            TdsEntries = await _db.TdsEntries.Where(t => t.TaxReturnId == returnId).ToListAsync(ct),
            Challans = await _db.TaxPaymentChallans.Where(c => c.TaxReturnId == returnId).ToListAsync(ct),
            AssetsLiabilities = await _db.AssetsLiabilities
                .FirstOrDefaultAsync(a => a.TaxReturnId == returnId && a.TenantId == ret.TenantId, ct),
            ForeignBankAccounts = await _db.ForeignBankAccounts
                .Where(f => f.TaxReturnId == returnId && f.TenantId == ret.TenantId).ToListAsync(ct),
            Donations80G = await _db.Donations80G
                .Where(d => d.TaxReturnId == returnId && d.TenantId == ret.TenantId).ToListAsync(ct),
            ExemptIncomes = await _db.ExemptIncomes
                .Where(e => e.TaxReturnId == returnId && e.TenantId == ret.TenantId).ToListAsync(ct),
            ForeignSourceIncomes = await _db.ForeignSourceIncomes
                .Where(f => f.TaxReturnId == returnId && f.TenantId == ret.TenantId).ToListAsync(ct),
            ClubbedIncomes = await _db.ClubbedIncomes
                .Where(s => s.TaxReturnId == returnId && s.TenantId == ret.TenantId).ToListAsync(ct),
            ImmovablePropertiesAL = await _db.ImmovablePropertiesAL
                .Where(p => p.TaxReturnId == returnId && p.TenantId == ret.TenantId).ToListAsync(ct),
            FirmInterestsAL = await _db.FirmInterestsAL
                .Where(f => f.TaxReturnId == returnId && f.TenantId == ret.TenantId).ToListAsync(ct),
            ForeignCustodialAccounts = await _db.ForeignCustodialAccounts
                .Where(c => c.TaxReturnId == returnId && c.TenantId == ret.TenantId).ToListAsync(ct),
            ForeignEquityDebtInterests = await _db.ForeignEquityDebtInterests
                .Where(e => e.TaxReturnId == returnId && e.TenantId == ret.TenantId).ToListAsync(ct),
            ForeignImmovableProperties = await _db.ForeignImmovableProperties
                .Where(p => p.TaxReturnId == returnId && p.TenantId == ret.TenantId).ToListAsync(ct),
            ForeignFinancialInterests = await _db.ForeignFinancialInterests
                .Where(f => f.TaxReturnId == returnId && f.TenantId == ret.TenantId).ToListAsync(ct),
            ForeignSigningAuthorities = await _db.ForeignSigningAuthorities
                .Where(s => s.TaxReturnId == returnId && s.TenantId == ret.TenantId).ToListAsync(ct),
            ForeignOtherIncomes = await _db.ForeignOtherIncomes
                .Where(o => o.TaxReturnId == returnId && o.TenantId == ret.TenantId).ToListAsync(ct),
            ForeignCashValueInsurances = await _db.ForeignCashValueInsurances
                .Where(c => c.TaxReturnId == returnId && c.TenantId == ret.TenantId).ToListAsync(ct),
            ForeignOtherAssets = await _db.ForeignOtherAssets
                .Where(a => a.TaxReturnId == returnId && a.TenantId == ret.TenantId).ToListAsync(ct),
            ForeignTrustInterests = await _db.ForeignTrustInterests
                .Where(t => t.TaxReturnId == returnId && t.TenantId == ret.TenantId).ToListAsync(ct),
        };
    }

    private async Task<ItrFiling> LoadOwnedFilingAsync(Guid fileId, CancellationToken ct)
        => await _db.ItrFilings.FirstOrDefaultAsync(
               f => f.Id == fileId && f.TenantId == _currentUser.TenantId && f.UserId == _currentUser.UserId, ct)
           ?? throw AppException.NotFound("ITR JSON file not found.", "ITRJSON.NOT_FOUND");

    private async Task EnsureOwnedReturnAsync(Guid returnId, CancellationToken ct)
    {
        var exists = await _db.TaxReturns.AnyAsync(
            r => r.Id == returnId && r.TenantId == _currentUser.TenantId && r.UserId == _currentUser.UserId, ct);
        if (!exists)
        {
            throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");
        }
    }

    private static ItrJsonArtifactDto ToDto(ItrFiling f) => new(
        f.Id, f.TaxReturnId, f.AssessmentYearCode, f.ItrType, f.SchemaVersion, f.Status,
        f.Status == ItrFilingStatus.Valid, f.ErrorCount, f.WarningCount,
        FileName(f), Encoding.UTF8.GetByteCount(f.RawJson), f.JsonHash, f.GeneratedAt, f.ValidatedAt);

    private static string FileName(ItrFiling f)
        => $"{f.ItrType}_{f.AssessmentYearCode}.json".Replace(" ", string.Empty);

    private static string Sha256(string s)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
}
