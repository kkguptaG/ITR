using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.PassThroughIncomes;

public interface IPassThroughIncomeService
{
    Task<IReadOnlyList<PassThroughIncomeDto>> ListAsync(Guid returnId, CancellationToken ct = default);
    Task<PassThroughIncomeDto> AddAsync(Guid returnId, UpsertPassThroughIncomeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Pass-through income components (Schedule PTI rows for ITR-2/3). Return-scoped, owner/tenant-scoped.
/// Scrutor binds PassThroughIncomeService : IPassThroughIncomeService scoped.
/// </summary>
public sealed class PassThroughIncomeService : IPassThroughIncomeService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public PassThroughIncomeService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PassThroughIncomeDto>> ListAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await Query(returnId).OrderBy(p => p.BusinessName).ThenBy(p => p.Category).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<PassThroughIncomeDto> AddAsync(Guid returnId, UpsertPassThroughIncomeRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new PassThroughIncome
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            BusinessName = r.BusinessName.Trim(),
            BusinessPan = r.BusinessPan.Trim().ToUpperInvariant(),
            InvestmentType = r.InvestmentType,
            Category = r.Category,
            AmountOfIncome = Clamp(r.AmountOfIncome),
            CurrentYearLossShare = Clamp(r.CurrentYearLossShare),
            TdsAmount = Clamp(r.TdsAmount),
        };
        _db.PassThroughIncomes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await Query(returnId).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw AppException.NotFound("Pass-through income item not found.", "PASSTHROUGHINCOME.NOT_FOUND");
        _db.PassThroughIncomes.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<PassThroughIncome> Query(Guid returnId)
        => _db.PassThroughIncomes.Where(p => p.TaxReturnId == returnId
                                             && p.TenantId == _currentUser.TenantId && p.UserId == _currentUser.UserId);

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

    private static PassThroughIncomeDto ToDto(PassThroughIncome p) => new(
        p.Id, p.BusinessName, p.BusinessPan, p.InvestmentType, p.Category,
        p.AmountOfIncome, p.CurrentYearLossShare, p.TdsAmount);
}
