using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Depreciation;

public interface IUnabsorbedDepreciationService
{
    Task<IReadOnlyList<UnabsorbedDepreciationDto>> ListAsync(Guid returnId, CancellationToken ct = default);
    Task<UnabsorbedDepreciationDto> AddAsync(Guid returnId, UpsertUnabsorbedDepreciationRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Brought-forward unabsorbed depreciation rows (Schedule UD, ITR-3). Return-scoped, owner/tenant-scoped.
/// Scrutor binds UnabsorbedDepreciationService : IUnabsorbedDepreciationService scoped.
/// </summary>
public sealed class UnabsorbedDepreciationService : IUnabsorbedDepreciationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UnabsorbedDepreciationService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<UnabsorbedDepreciationDto>> ListAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await Query(returnId).OrderBy(u => u.AssessmentYearLabel).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<UnabsorbedDepreciationDto> AddAsync(Guid returnId, UpsertUnabsorbedDepreciationRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new UnabsorbedDepreciation
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            AssessmentYearLabel = r.AssessmentYearLabel.Trim(),
            UnabsorbedDepreciationAmount = Clamp(r.UnabsorbedDepreciationAmount),
            UnabsorbedAllowanceAmount = Clamp(r.UnabsorbedAllowanceAmount),
        };
        _db.UnabsorbedDepreciations.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await Query(returnId).FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw AppException.NotFound("Unabsorbed depreciation row not found.", "UNABSORBEDDEP.NOT_FOUND");
        _db.UnabsorbedDepreciations.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<UnabsorbedDepreciation> Query(Guid returnId)
        => _db.UnabsorbedDepreciations.Where(u => u.TaxReturnId == returnId
                                                  && u.TenantId == _currentUser.TenantId && u.UserId == _currentUser.UserId);

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

    private static UnabsorbedDepreciationDto ToDto(UnabsorbedDepreciation u) => new(
        u.Id, u.AssessmentYearLabel, u.UnabsorbedDepreciationAmount, u.UnabsorbedAllowanceAmount);
}
