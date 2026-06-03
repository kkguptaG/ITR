using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Depreciation;

public interface IDepreciableAssetService
{
    Task<IReadOnlyList<DepreciableAssetDto>> ListAsync(Guid returnId, CancellationToken ct = default);
    Task<DepreciableAssetDto> AddAsync(Guid returnId, UpsertDepreciableAssetRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Depreciable plant &amp; machinery blocks (Schedule DPM, ITR-3). Return-scoped, owner/tenant-scoped.
/// Scrutor binds DepreciableAssetService : IDepreciableAssetService scoped.
/// </summary>
public sealed class DepreciableAssetService : IDepreciableAssetService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public DepreciableAssetService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<DepreciableAssetDto>> ListAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await Query(returnId).OrderBy(a => a.Category).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<DepreciableAssetDto> AddAsync(Guid returnId, UpsertDepreciableAssetRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new DepreciableAsset
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            Category = r.Category,
            OpeningWdv = Clamp(r.OpeningWdv),
            AdditionsAbove180Days = Clamp(r.AdditionsAbove180Days),
            AdditionsBelow180Days = Clamp(r.AdditionsBelow180Days),
            SaleProceeds = Clamp(r.SaleProceeds),
            BookDepreciation = Clamp(r.BookDepreciation),
        };
        _db.DepreciableAssets.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await Query(returnId).FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw AppException.NotFound("Depreciation block not found.", "DEPRECIABLEASSET.NOT_FOUND");
        _db.DepreciableAssets.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<DepreciableAsset> Query(Guid returnId)
        => _db.DepreciableAssets.Where(a => a.TaxReturnId == returnId
                                            && a.TenantId == _currentUser.TenantId && a.UserId == _currentUser.UserId);

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

    private static DepreciableAssetDto ToDto(DepreciableAsset a) => new(
        a.Id, a.Category, a.OpeningWdv, a.AdditionsAbove180Days, a.AdditionsBelow180Days, a.SaleProceeds, a.BookDepreciation);
}
