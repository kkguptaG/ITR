using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.BankAccounts;

public interface IBankAccountService
{
    Task<IReadOnlyList<BankAccountDto>> ListAsync(CancellationToken ct = default);
    Task<BankAccountDto> AddAsync(UpsertBankAccountRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<BankAccountDto> SetForRefundAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// The assessee's bank accounts (user-scoped). All four fields are mandatory and exactly one account is
/// the refund account — adding/selecting one clears the flag on the others. Scrutor binds
/// BankAccountService : IBankAccountService scoped (no manual DI).
/// </summary>
public sealed class BankAccountService : IBankAccountService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public BankAccountService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<BankAccountDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await Query().ToListAsync(ct);
        return rows.OrderByDescending(b => b.UseForRefund).ThenBy(b => b.BankName).Select(ToDto).ToList();
    }

    public async Task<BankAccountDto> AddAsync(UpsertBankAccountRequest request, CancellationToken ct = default)
    {
        var existing = await Query().ToListAsync(ct);
        var entity = new BankAccountDetail
        {
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            BankName = request.BankName.Trim(),
            AccountNumber = request.AccountNumber.Trim(),
            AccountType = request.AccountType.Trim().ToUpperInvariant(),
            Ifsc = request.Ifsc.Trim().ToUpperInvariant(),
            UseForRefund = request.UseForRefund || existing.Count == 0, // first account is the refund by default
        };
        if (entity.UseForRefund)
        {
            foreach (var e in existing)
            {
                e.UseForRefund = false; // exactly one refund account
            }
        }

        _db.BankAccountDetails.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await Query().FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw AppException.NotFound("Bank account not found.", "BANK.NOT_FOUND");
        var wasRefund = entity.UseForRefund;
        _db.BankAccountDetails.Remove(entity);
        await _db.SaveChangesAsync(ct);

        // Keep exactly one refund account: if we removed it, promote another.
        if (wasRefund)
        {
            var rest = await Query().OrderBy(b => b.BankName).ToListAsync(ct);
            if (rest.Count > 0)
            {
                rest[0].UseForRefund = true;
                await _db.SaveChangesAsync(ct);
            }
        }
    }

    public async Task<BankAccountDto> SetForRefundAsync(Guid id, CancellationToken ct = default)
    {
        var all = await Query().ToListAsync(ct);
        var target = all.FirstOrDefault(b => b.Id == id)
            ?? throw AppException.NotFound("Bank account not found.", "BANK.NOT_FOUND");
        foreach (var b in all)
        {
            b.UseForRefund = b.Id == id;
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(target);
    }

    private IQueryable<BankAccountDetail> Query()
        => _db.BankAccountDetails.Where(b => b.TenantId == _currentUser.TenantId && b.UserId == _currentUser.UserId);

    private static BankAccountDto ToDto(BankAccountDetail b)
        => new(b.Id, b.BankName, MaskAccount(b.AccountNumber), b.AccountType, b.Ifsc, b.UseForRefund);

    private static string MaskAccount(string acc)
        => string.IsNullOrEmpty(acc) || acc.Length <= 4 ? acc : new string('X', acc.Length - 4) + acc[^4..];
}
