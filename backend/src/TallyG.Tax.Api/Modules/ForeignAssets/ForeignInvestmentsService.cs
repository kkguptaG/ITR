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

    Task<IReadOnlyList<ForeignImmovablePropertyFaDto>> ListImmovableAsync(Guid returnId, CancellationToken ct = default);
    Task<ForeignImmovablePropertyFaDto> AddImmovableAsync(Guid returnId, UpsertForeignImmovablePropertyFaRequest request, CancellationToken ct = default);
    Task DeleteImmovableAsync(Guid returnId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ForeignFinancialInterestDto>> ListFinancialInterestAsync(Guid returnId, CancellationToken ct = default);
    Task<ForeignFinancialInterestDto> AddFinancialInterestAsync(Guid returnId, UpsertForeignFinancialInterestRequest request, CancellationToken ct = default);
    Task DeleteFinancialInterestAsync(Guid returnId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ForeignCashValueInsuranceDto>> ListCashValueAsync(Guid returnId, CancellationToken ct = default);
    Task<ForeignCashValueInsuranceDto> AddCashValueAsync(Guid returnId, UpsertForeignCashValueInsuranceRequest request, CancellationToken ct = default);
    Task DeleteCashValueAsync(Guid returnId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ForeignOtherAssetDto>> ListOtherAssetAsync(Guid returnId, CancellationToken ct = default);
    Task<ForeignOtherAssetDto> AddOtherAssetAsync(Guid returnId, UpsertForeignOtherAssetRequest request, CancellationToken ct = default);
    Task DeleteOtherAssetAsync(Guid returnId, Guid id, CancellationToken ct = default);
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

    // ----------------------------------------------------------------- immovable property (abroad)
    public async Task<IReadOnlyList<ForeignImmovablePropertyFaDto>> ListImmovableAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await ImmovableQuery(returnId).OrderBy(p => p.CountryName).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ForeignImmovablePropertyFaDto> AddImmovableAsync(Guid returnId, UpsertForeignImmovablePropertyFaRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ForeignImmovablePropertyFA
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            CountryCode = r.CountryCode.Trim(), CountryName = r.CountryName.Trim(), ZipCode = r.ZipCode.Trim(),
            AddressOfProperty = r.AddressOfProperty.Trim(),
            Ownership = r.Ownership.Trim().ToUpperInvariant(),
            AcquisitionDate = r.AcquisitionDate,
            TotalInvestment = Clamp(r.TotalInvestment),
            IncomeDerived = Clamp(r.IncomeDerived),
            NatureOfIncome = r.NatureOfIncome.Trim(),
            TaxableIncomeAmount = Clamp(r.TaxableIncomeAmount),
            IncomeTaxSchedule = r.IncomeTaxSchedule.Trim().ToUpperInvariant(),
            IncomeTaxScheduleItem = r.IncomeTaxScheduleItem.Trim(),
        };
        _db.ForeignImmovableProperties.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteImmovableAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await ImmovableQuery(returnId).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw AppException.NotFound("Foreign property not found.", "FOREIGNASSET.NOT_FOUND");
        _db.ForeignImmovableProperties.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- financial interest in an entity
    public async Task<IReadOnlyList<ForeignFinancialInterestDto>> ListFinancialInterestAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await FinancialQuery(returnId).OrderBy(f => f.CountryName).ThenBy(f => f.EntityName).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ForeignFinancialInterestDto> AddFinancialInterestAsync(Guid returnId, UpsertForeignFinancialInterestRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ForeignFinancialInterest
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            CountryCode = r.CountryCode.Trim(), CountryName = r.CountryName.Trim(), ZipCode = r.ZipCode.Trim(),
            NatureOfEntity = r.NatureOfEntity.Trim(),
            EntityName = r.EntityName.Trim(), EntityAddress = r.EntityAddress.Trim(),
            NatureOfInterest = r.NatureOfInterest.Trim().ToUpperInvariant(),
            DateHeld = r.DateHeld,
            TotalInvestment = Clamp(r.TotalInvestment),
            IncomeFromInterest = Clamp(r.IncomeFromInterest),
            NatureOfIncome = r.NatureOfIncome.Trim(),
            TaxableIncomeAmount = Clamp(r.TaxableIncomeAmount),
            IncomeTaxSchedule = r.IncomeTaxSchedule.Trim().ToUpperInvariant(),
            IncomeTaxScheduleItem = r.IncomeTaxScheduleItem.Trim(),
        };
        _db.ForeignFinancialInterests.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteFinancialInterestAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await FinancialQuery(returnId).FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw AppException.NotFound("Financial interest not found.", "FOREIGNASSET.NOT_FOUND");
        _db.ForeignFinancialInterests.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- cash-value insurance
    public async Task<IReadOnlyList<ForeignCashValueInsuranceDto>> ListCashValueAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await CashValueQuery(returnId).OrderBy(c => c.CountryName).ThenBy(c => c.InstitutionName).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ForeignCashValueInsuranceDto> AddCashValueAsync(Guid returnId, UpsertForeignCashValueInsuranceRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ForeignCashValueInsurance
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            CountryCode = r.CountryCode.Trim(), CountryName = r.CountryName.Trim(), ZipCode = r.ZipCode.Trim(),
            InstitutionName = r.InstitutionName.Trim(), InstitutionAddress = r.InstitutionAddress.Trim(),
            ContractDate = r.ContractDate,
            CashOrSurrenderValue = Clamp(r.CashOrSurrenderValue),
            GrossAmountCredited = Clamp(r.GrossAmountCredited),
        };
        _db.ForeignCashValueInsurances.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteCashValueAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await CashValueQuery(returnId).FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw AppException.NotFound("Insurance contract not found.", "FOREIGNASSET.NOT_FOUND");
        _db.ForeignCashValueInsurances.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- other capital assets
    public async Task<IReadOnlyList<ForeignOtherAssetDto>> ListOtherAssetAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await OtherAssetQuery(returnId).OrderBy(a => a.CountryName).ThenBy(a => a.NatureOfAsset).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ForeignOtherAssetDto> AddOtherAssetAsync(Guid returnId, UpsertForeignOtherAssetRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ForeignOtherAsset
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            CountryCode = r.CountryCode.Trim(), CountryName = r.CountryName.Trim(), ZipCode = r.ZipCode.Trim(),
            NatureOfAsset = r.NatureOfAsset.Trim(),
            Ownership = r.Ownership.Trim().ToUpperInvariant(),
            AcquisitionDate = r.AcquisitionDate,
            TotalInvestment = Clamp(r.TotalInvestment),
            IncomeDerived = Clamp(r.IncomeDerived),
            NatureOfIncome = r.NatureOfIncome.Trim(),
            TaxableIncomeAmount = Clamp(r.TaxableIncomeAmount),
            IncomeTaxSchedule = r.IncomeTaxSchedule.Trim().ToUpperInvariant(),
            IncomeTaxScheduleItem = r.IncomeTaxScheduleItem.Trim(),
        };
        _db.ForeignOtherAssets.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteOtherAssetAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await OtherAssetQuery(returnId).FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw AppException.NotFound("Foreign asset not found.", "FOREIGNASSET.NOT_FOUND");
        _db.ForeignOtherAssets.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<ForeignCashValueInsurance> CashValueQuery(Guid returnId)
        => _db.ForeignCashValueInsurances.Where(c => c.TaxReturnId == returnId
                                                    && c.TenantId == _currentUser.TenantId && c.UserId == _currentUser.UserId);

    private IQueryable<ForeignOtherAsset> OtherAssetQuery(Guid returnId)
        => _db.ForeignOtherAssets.Where(a => a.TaxReturnId == returnId
                                            && a.TenantId == _currentUser.TenantId && a.UserId == _currentUser.UserId);

    private IQueryable<ForeignImmovablePropertyFA> ImmovableQuery(Guid returnId)
        => _db.ForeignImmovableProperties.Where(p => p.TaxReturnId == returnId
                                                    && p.TenantId == _currentUser.TenantId && p.UserId == _currentUser.UserId);

    private IQueryable<ForeignFinancialInterest> FinancialQuery(Guid returnId)
        => _db.ForeignFinancialInterests.Where(f => f.TaxReturnId == returnId
                                                   && f.TenantId == _currentUser.TenantId && f.UserId == _currentUser.UserId);

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

    private static ForeignImmovablePropertyFaDto ToDto(ForeignImmovablePropertyFA p) => new(
        p.Id, p.CountryCode, p.CountryName, p.ZipCode, p.AddressOfProperty, p.Ownership, p.AcquisitionDate,
        p.TotalInvestment, p.IncomeDerived, p.NatureOfIncome, p.TaxableIncomeAmount, p.IncomeTaxSchedule, p.IncomeTaxScheduleItem);

    private static ForeignFinancialInterestDto ToDto(ForeignFinancialInterest f) => new(
        f.Id, f.CountryCode, f.CountryName, f.ZipCode, f.NatureOfEntity, f.EntityName, f.EntityAddress, f.NatureOfInterest,
        f.DateHeld, f.TotalInvestment, f.IncomeFromInterest, f.NatureOfIncome, f.TaxableIncomeAmount, f.IncomeTaxSchedule, f.IncomeTaxScheduleItem);

    private static ForeignCashValueInsuranceDto ToDto(ForeignCashValueInsurance c) => new(
        c.Id, c.CountryCode, c.CountryName, c.InstitutionName, c.InstitutionAddress, c.ZipCode,
        c.ContractDate, c.CashOrSurrenderValue, c.GrossAmountCredited);

    private static ForeignOtherAssetDto ToDto(ForeignOtherAsset a) => new(
        a.Id, a.CountryCode, a.CountryName, a.ZipCode, a.NatureOfAsset, a.Ownership, a.AcquisitionDate,
        a.TotalInvestment, a.IncomeDerived, a.NatureOfIncome, a.TaxableIncomeAmount, a.IncomeTaxSchedule, a.IncomeTaxScheduleItem);
}
