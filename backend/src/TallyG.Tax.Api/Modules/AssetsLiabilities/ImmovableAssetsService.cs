using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.AssetsLiabilities;

public interface IImmovableAssetsService
{
    Task<IReadOnlyList<ImmovablePropertyAlDto>> ListAsync(Guid returnId, CancellationToken ct = default);
    Task<ImmovablePropertyAlDto> AddAsync(Guid returnId, UpsertImmovablePropertyAlRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Immovable properties for Schedule AL's ImmovableDetails list. Return-scoped, owner/tenant-scoped.
/// Scrutor binds ImmovableAssetsService : IImmovableAssetsService scoped.
/// </summary>
public sealed class ImmovableAssetsService : IImmovableAssetsService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ImmovableAssetsService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ImmovablePropertyAlDto>> ListAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await Query(returnId).OrderBy(p => p.Description).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ImmovablePropertyAlDto> AddAsync(Guid returnId, UpsertImmovablePropertyAlRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ImmovablePropertyAL
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            Description = r.Description.Trim(),
            FlatDoorNo = r.FlatDoorNo.Trim(),
            Locality = r.Locality.Trim(),
            City = r.City.Trim(),
            StateCode = r.StateCode.Trim(),
            Pincode = r.Pincode.Trim(),
            Cost = Clamp(r.Cost),
        };
        _db.ImmovablePropertiesAL.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await Query(returnId).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw AppException.NotFound("Immovable property not found.", "IMMOVABLEAL.NOT_FOUND");
        _db.ImmovablePropertiesAL.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<ImmovablePropertyAL> Query(Guid returnId)
        => _db.ImmovablePropertiesAL.Where(p => p.TaxReturnId == returnId
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

    private static ImmovablePropertyAlDto ToDto(ImmovablePropertyAL p) => new(
        p.Id, p.Description, p.FlatDoorNo, p.Locality, p.City, p.StateCode, p.Pincode, p.Cost);
}
