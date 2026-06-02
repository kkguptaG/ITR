using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.ForeignAssets;

public interface IForeignInvestmentsService
{
    Task<IReadOnlyList<ForeignCustodialAccountDto>> ListCustodialAsync(Guid returnId, CancellationToken ct = default);
    Task<ForeignCustodialAccountDto> AddCustodialAsync(Guid returnId, UpsertForeignCustodialAccountRequest request, CancellationToken ct = default);
    Task DeleteCustodialAsync(Guid returnId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ForeignEquityDebtInterestDto>> ListEquityDebtAsync(Guid returnId, CancellationToken ct = default);
    Task<ForeignEquityDebtInterestDto> AddEquityDebtAsync(Guid returnId, UpsertForeignEquityDebtInterestRequest request, CancellationToken ct = default);
    Task DeleteEquityDebtAsync(Guid returnId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Foreign custodial/brokerage accounts + equity/debt interests (Schedule FA). Return-scoped,
/// owner/tenant-scoped. Scrutor binds ForeignInvestmentsService : IForeignInvestmentsService scoped.
/// </summary>
public sealed class ForeignInvestmentsService : IForeignInvestmentsService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ForeignInvestmentsService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ----------------------------------------------------------------- custodial accounts
    public async Task<IReadOnlyList<ForeignCustodialAccountDto>> ListCustodialAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await CustodialQuery(returnId).OrderBy(c => c.CountryName).ThenBy(c => c.InstitutionName).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ForeignCustodialAccountDto> AddCustodialAsync(Guid returnId, UpsertForeignCustodialAccountRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ForeignCustodialAccount
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            CountryCode = r.CountryCode.Trim(),
            CountryName = r.CountryName.Trim(),
            InstitutionName = r.InstitutionName.Trim(),
            InstitutionAddress = r.InstitutionAddress.Trim(),
            ZipCode = r.ZipCode.Trim(),
            AccountNumber = r.AccountNumber.Trim(),
            Status = r.Status.Trim(),
            AccountOpenDate = r.AccountOpenDate,
            PeakBalance = Clamp(r.PeakBalance),
            ClosingBalance = Clamp(r.ClosingBalance),
            GrossAmountCredited = Clamp(r.GrossAmountCredited),
            NatureOfAmount = r.NatureOfAmount.Trim(),
        };
        _db.ForeignCustodialAccounts.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteCustodialAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await CustodialQuery(returnId).FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw AppException.NotFound("Custodial account not found.", "FOREIGNASSET.NOT_FOUND");
        _db.ForeignCustodialAccounts.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- equity/debt interests
    public async Task<IReadOnlyList<ForeignEquityDebtInterestDto>> ListEquityDebtAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await EquityQuery(returnId).OrderBy(e => e.CountryName).ThenBy(e => e.EntityName).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ForeignEquityDebtInterestDto> AddEquityDebtAsync(Guid returnId, UpsertForeignEquityDebtInterestRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ForeignEquityDebtInterest
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            CountryCode = r.CountryCode.Trim(),
            CountryName = r.CountryName.Trim(),
            EntityName = r.EntityName.Trim(),
            EntityAddress = r.EntityAddress.Trim(),
            ZipCode = r.ZipCode.Trim(),
            NatureOfEntity = r.NatureOfEntity.Trim(),
            AcquisitionDate = r.AcquisitionDate,
            InitialValue = Clamp(r.InitialValue),
            PeakBalance = Clamp(r.PeakBalance),
            ClosingBalance = Clamp(r.ClosingBalance),
            GrossAmountCredited = Clamp(r.GrossAmountCredited),
            GrossProceeds = Clamp(r.GrossProceeds),
        };
        _db.ForeignEquityDebtInterests.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteEquityDebtAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await EquityQuery(returnId).FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw AppException.NotFound("Equity/debt interest not found.", "FOREIGNASSET.NOT_FOUND");
        _db.ForeignEquityDebtInterests.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<ForeignCustodialAccount> CustodialQuery(Guid returnId)
        => _db.ForeignCustodialAccounts.Where(c => c.TaxReturnId == returnId
                                                   && c.TenantId == _currentUser.TenantId && c.UserId == _currentUser.UserId);

    private IQueryable<ForeignEquityDebtInterest> EquityQuery(Guid returnId)
        => _db.ForeignEquityDebtInterests.Where(e => e.TaxReturnId == returnId
                                                    && e.TenantId == _currentUser.TenantId && e.UserId == _currentUser.UserId);

    private async Task<TaxReturn> OwnedReturnAsync(Guid returnId, CancellationToken ct)
        => await _db.TaxReturns.FirstOrDefaultAsync(
               r => r.Id == returnId && r.TenantId == _currentUser.TenantId && r.UserId == _currentUser.UserId, ct)
           ?? throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");

    private async Task EnsureOwnedReturnAsync(Guid returnId, CancellationToken ct)
    {
        if (!await _db.TaxReturns.AnyAsync(
                r => r.Id == returnId && r.TenantId == _currentUser.TenantId && r.UserId == _currentUser.UserId, ct))
        {
            throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");
        }
    }

    private static decimal Clamp(decimal v) => Math.Clamp(v, 0m, 99_999_999_999_999m);

    private static string Mask(string acc)
        => string.IsNullOrEmpty(acc) || acc.Length <= 4 ? acc : new string('X', acc.Length - 4) + acc[^4..];

    private static ForeignCustodialAccountDto ToDto(ForeignCustodialAccount c) => new(
        c.Id, c.CountryCode, c.CountryName, c.InstitutionName, c.InstitutionAddress, c.ZipCode,
        Mask(c.AccountNumber), c.Status, c.AccountOpenDate, c.PeakBalance, c.ClosingBalance, c.GrossAmountCredited, c.NatureOfAmount);

    private static ForeignEquityDebtInterestDto ToDto(ForeignEquityDebtInterest e) => new(
        e.Id, e.CountryCode, e.CountryName, e.EntityName, e.EntityAddress, e.ZipCode, e.NatureOfEntity,
        e.AcquisitionDate, e.InitialValue, e.PeakBalance, e.ClosingBalance, e.GrossAmountCredited, e.GrossProceeds);
}
