using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.ForeignAssets;

public interface IForeignBankAccountService
{
    Task<IReadOnlyList<ForeignBankAccountDto>> ListAsync(Guid returnId, CancellationToken ct = default);
    Task<ForeignBankAccountDto> AddAsync(Guid returnId, UpsertForeignBankAccountRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Foreign bank/depository accounts disclosed in Schedule FA. Return-scoped (balances are per AY),
/// owner/tenant-scoped. Scrutor binds ForeignBankAccountService : IForeignBankAccountService scoped.
/// </summary>
public sealed class ForeignBankAccountService : IForeignBankAccountService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ForeignBankAccountService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ForeignBankAccountDto>> ListAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await Query(returnId).OrderBy(f => f.CountryName).ThenBy(f => f.BankName).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ForeignBankAccountDto> AddAsync(Guid returnId, UpsertForeignBankAccountRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ForeignBankAccount
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            CountryCode = r.CountryCode.Trim(),
            CountryName = r.CountryName.Trim(),
            BankName = r.BankName.Trim(),
            Address = r.Address.Trim(),
            ZipCode = r.ZipCode.Trim(),
            AccountNumber = r.AccountNumber.Trim(),
            OwnerStatus = r.OwnerStatus.Trim().ToUpperInvariant(),
            AccountOpenDate = r.AccountOpenDate,
            PeakBalance = Clamp(r.PeakBalance),
            ClosingBalance = Clamp(r.ClosingBalance),
            InterestAccrued = Clamp(r.InterestAccrued),
        };
        _db.ForeignBankAccounts.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await Query(returnId).FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw AppException.NotFound("Foreign account not found.", "FOREIGNASSET.NOT_FOUND");
        _db.ForeignBankAccounts.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<ForeignBankAccount> Query(Guid returnId)
        => _db.ForeignBankAccounts.Where(f => f.TaxReturnId == returnId
                                              && f.TenantId == _currentUser.TenantId && f.UserId == _currentUser.UserId);

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

    private static ForeignBankAccountDto ToDto(ForeignBankAccount f) => new(
        f.Id, f.CountryCode, f.CountryName, f.BankName, f.Address, f.ZipCode,
        Mask(f.AccountNumber), f.OwnerStatus, f.AccountOpenDate, f.PeakBalance, f.ClosingBalance, f.InterestAccrued);

    private static string Mask(string acc)
        => string.IsNullOrEmpty(acc) || acc.Length <= 4 ? acc : new string('X', acc.Length - 4) + acc[^4..];
}
