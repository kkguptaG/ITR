using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.ClubbedIncomes;

public interface IClubbedIncomeService
{
    Task<IReadOnlyList<ClubbedIncomeDto>> ListAsync(Guid returnId, CancellationToken ct = default);
    Task<ClubbedIncomeDto> AddAsync(Guid returnId, UpsertClubbedIncomeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Clubbed income of specified persons (Schedule SPI rows for ITR-2/3). Return-scoped, owner/tenant-scoped.
/// Scrutor binds ClubbedIncomeService : IClubbedIncomeService scoped.
/// </summary>
public sealed class ClubbedIncomeService : IClubbedIncomeService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ClubbedIncomeService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ClubbedIncomeDto>> ListAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await Query(returnId).OrderBy(s => s.SpecifiedPersonName).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ClubbedIncomeDto> AddAsync(Guid returnId, UpsertClubbedIncomeRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ClubbedIncome
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            SpecifiedPersonName = r.SpecifiedPersonName.Trim(),
            Pan = string.IsNullOrWhiteSpace(r.Pan) ? null : r.Pan.Trim().ToUpperInvariant(),
            Aadhaar = string.IsNullOrWhiteSpace(r.Aadhaar) ? null : r.Aadhaar.Trim(),
            Relationship = r.Relationship.Trim(),
            AmountIncluded = Clamp(r.AmountIncluded),
            IncomeHead = r.IncomeHead,
        };
        _db.ClubbedIncomes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await Query(returnId).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw AppException.NotFound("Clubbed income item not found.", "CLUBBEDINCOME.NOT_FOUND");
        _db.ClubbedIncomes.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<ClubbedIncome> Query(Guid returnId)
        => _db.ClubbedIncomes.Where(s => s.TaxReturnId == returnId
                                         && s.TenantId == _currentUser.TenantId && s.UserId == _currentUser.UserId);

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

    private static ClubbedIncomeDto ToDto(ClubbedIncome s) => new(
        s.Id, s.SpecifiedPersonName, s.Pan, s.Aadhaar, s.Relationship, s.AmountIncluded, s.IncomeHead);
}
