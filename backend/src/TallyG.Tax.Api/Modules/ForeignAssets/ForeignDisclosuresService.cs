using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.ForeignAssets;

public interface IForeignDisclosuresService
{
    Task<IReadOnlyList<ForeignSigningAuthorityDto>> ListSigningAuthAsync(Guid returnId, CancellationToken ct = default);
    Task<ForeignSigningAuthorityDto> AddSigningAuthAsync(Guid returnId, UpsertForeignSigningAuthorityRequest request, CancellationToken ct = default);
    Task DeleteSigningAuthAsync(Guid returnId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ForeignOtherIncomeDto>> ListOtherIncomeAsync(Guid returnId, CancellationToken ct = default);
    Task<ForeignOtherIncomeDto> AddOtherIncomeAsync(Guid returnId, UpsertForeignOtherIncomeRequest request, CancellationToken ct = default);
    Task DeleteOtherIncomeAsync(Guid returnId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ForeignTrustInterestDto>> ListTrustAsync(Guid returnId, CancellationToken ct = default);
    Task<ForeignTrustInterestDto> AddTrustAsync(Guid returnId, UpsertForeignTrustInterestRequest request, CancellationToken ct = default);
    Task DeleteTrustAsync(Guid returnId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Schedule FA signing-authority accounts + other-income-outside-India. Return-scoped, owner/tenant-scoped.
/// Scrutor binds ForeignDisclosuresService : IForeignDisclosuresService scoped.
/// </summary>
public sealed class ForeignDisclosuresService : IForeignDisclosuresService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ForeignDisclosuresService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ----------------------------------------------------------------- signing authority
    public async Task<IReadOnlyList<ForeignSigningAuthorityDto>> ListSigningAuthAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await SigningQuery(returnId).OrderBy(s => s.CountryName).ThenBy(s => s.InstitutionName).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ForeignSigningAuthorityDto> AddSigningAuthAsync(Guid returnId, UpsertForeignSigningAuthorityRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ForeignSigningAuthority
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            CountryCode = r.CountryCode.Trim(), CountryName = r.CountryName.Trim(), ZipCode = r.ZipCode.Trim(),
            InstitutionName = r.InstitutionName.Trim(), InstitutionAddress = r.InstitutionAddress.Trim(),
            AccountHolderName = r.AccountHolderName.Trim(), AccountNumber = r.AccountNumber.Trim(),
            PeakBalanceOrInvestment = Clamp(r.PeakBalanceOrInvestment),
            IncomeTaxable = r.IncomeTaxable,
            IncomeAccrued = Clamp(r.IncomeAccrued),
            IncomeOffered = Clamp(r.IncomeOffered),
            IncomeTaxSchedule = r.IncomeTaxSchedule.Trim().ToUpperInvariant(),
            IncomeTaxScheduleItem = r.IncomeTaxScheduleItem.Trim(),
        };
        _db.ForeignSigningAuthorities.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteSigningAuthAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await SigningQuery(returnId).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw AppException.NotFound("Signing-authority account not found.", "FOREIGNASSET.NOT_FOUND");
        _db.ForeignSigningAuthorities.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- other income outside India
    public async Task<IReadOnlyList<ForeignOtherIncomeDto>> ListOtherIncomeAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await OtherIncomeQuery(returnId).OrderBy(o => o.CountryName).ThenBy(o => o.PayerName).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ForeignOtherIncomeDto> AddOtherIncomeAsync(Guid returnId, UpsertForeignOtherIncomeRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ForeignOtherIncome
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            CountryCode = r.CountryCode.Trim(), CountryName = r.CountryName.Trim(), ZipCode = r.ZipCode.Trim(),
            PayerName = r.PayerName.Trim(), PayerAddress = r.PayerAddress.Trim(),
            IncomeDerived = Clamp(r.IncomeDerived),
            NatureOfIncome = r.NatureOfIncome.Trim(),
            IncomeTaxable = r.IncomeTaxable,
            IncomeOffered = Clamp(r.IncomeOffered),
            IncomeTaxSchedule = r.IncomeTaxSchedule.Trim().ToUpperInvariant(),
            IncomeTaxScheduleItem = r.IncomeTaxScheduleItem.Trim(),
        };
        _db.ForeignOtherIncomes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteOtherIncomeAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await OtherIncomeQuery(returnId).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw AppException.NotFound("Foreign income not found.", "FOREIGNASSET.NOT_FOUND");
        _db.ForeignOtherIncomes.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- trusts outside India
    public async Task<IReadOnlyList<ForeignTrustInterestDto>> ListTrustAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await TrustQuery(returnId).OrderBy(t => t.CountryName).ThenBy(t => t.TrustName).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ForeignTrustInterestDto> AddTrustAsync(Guid returnId, UpsertForeignTrustInterestRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ForeignTrustInterest
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            CountryCode = r.CountryCode.Trim(), CountryName = r.CountryName.Trim(), ZipCode = r.ZipCode.Trim(),
            TrustName = r.TrustName.Trim(), TrustAddress = r.TrustAddress.Trim(),
            TrusteeNames = r.TrusteeNames.Trim(), TrusteeAddresses = r.TrusteeAddresses.Trim(),
            SettlorName = r.SettlorName.Trim(), SettlorAddress = r.SettlorAddress.Trim(),
            BeneficiaryNames = r.BeneficiaryNames.Trim(), BeneficiaryAddresses = r.BeneficiaryAddresses.Trim(),
            DateHeld = r.DateHeld,
            IncomeTaxable = r.IncomeTaxable,
            IncomeFromTrust = Clamp(r.IncomeFromTrust),
            IncomeOffered = Clamp(r.IncomeOffered),
            IncomeTaxSchedule = r.IncomeTaxSchedule.Trim().ToUpperInvariant(),
            IncomeTaxScheduleItem = r.IncomeTaxScheduleItem.Trim(),
        };
        _db.ForeignTrustInterests.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteTrustAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await TrustQuery(returnId).FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw AppException.NotFound("Trust interest not found.", "FOREIGNASSET.NOT_FOUND");
        _db.ForeignTrustInterests.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<ForeignTrustInterest> TrustQuery(Guid returnId)
        => _db.ForeignTrustInterests.Where(t => t.TaxReturnId == returnId
                                               && t.TenantId == _currentUser.TenantId && t.UserId == _currentUser.UserId);

    private IQueryable<ForeignSigningAuthority> SigningQuery(Guid returnId)
        => _db.ForeignSigningAuthorities.Where(s => s.TaxReturnId == returnId
                                                   && s.TenantId == _currentUser.TenantId && s.UserId == _currentUser.UserId);

    private IQueryable<ForeignOtherIncome> OtherIncomeQuery(Guid returnId)
        => _db.ForeignOtherIncomes.Where(o => o.TaxReturnId == returnId
                                             && o.TenantId == _currentUser.TenantId && o.UserId == _currentUser.UserId);

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

    private static ForeignSigningAuthorityDto ToDto(ForeignSigningAuthority s) => new(
        s.Id, s.CountryCode, s.CountryName, s.ZipCode, s.InstitutionName, s.InstitutionAddress,
        s.AccountHolderName, Mask(s.AccountNumber), s.PeakBalanceOrInvestment, s.IncomeTaxable,
        s.IncomeAccrued, s.IncomeOffered, s.IncomeTaxSchedule, s.IncomeTaxScheduleItem);

    private static ForeignOtherIncomeDto ToDto(ForeignOtherIncome o) => new(
        o.Id, o.CountryCode, o.CountryName, o.ZipCode, o.PayerName, o.PayerAddress, o.IncomeDerived,
        o.NatureOfIncome, o.IncomeTaxable, o.IncomeOffered, o.IncomeTaxSchedule, o.IncomeTaxScheduleItem);

    private static ForeignTrustInterestDto ToDto(ForeignTrustInterest t) => new(
        t.Id, t.CountryCode, t.CountryName, t.ZipCode, t.TrustName, t.TrustAddress, t.TrusteeNames, t.TrusteeAddresses,
        t.SettlorName, t.SettlorAddress, t.BeneficiaryNames, t.BeneficiaryAddresses, t.DateHeld, t.IncomeTaxable,
        t.IncomeFromTrust, t.IncomeOffered, t.IncomeTaxSchedule, t.IncomeTaxScheduleItem);
}
