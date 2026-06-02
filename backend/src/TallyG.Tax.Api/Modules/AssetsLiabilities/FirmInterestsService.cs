using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.AssetsLiabilities;

public interface IFirmInterestsService
{
    Task<IReadOnlyList<FirmInterestAlDto>> ListAsync(Guid returnId, CancellationToken ct = default);
    Task<FirmInterestAlDto> AddAsync(Guid returnId, UpsertFirmInterestAlRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Interests in a firm / AOP for Schedule AL's InterestHeldInaAsset list (ITR-3). Return-scoped,
/// owner/tenant-scoped. Scrutor binds FirmInterestsService : IFirmInterestsService scoped.
/// </summary>
public sealed class FirmInterestsService : IFirmInterestsService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public FirmInterestsService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<FirmInterestAlDto>> ListAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await Query(returnId).OrderBy(f => f.FirmName).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<FirmInterestAlDto> AddAsync(Guid returnId, UpsertFirmInterestAlRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new FirmInterestAL
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            FirmName = r.FirmName.Trim(),
            FirmPan = r.FirmPan.Trim().ToUpperInvariant(),
            FlatDoorNo = r.FlatDoorNo.Trim(),
            Locality = r.Locality.Trim(),
            City = r.City.Trim(),
            StateCode = r.StateCode.Trim(),
            Pincode = r.Pincode.Trim(),
            Investment = Clamp(r.Investment),
        };
        _db.FirmInterestsAL.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await Query(returnId).FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw AppException.NotFound("Firm interest not found.", "FIRMINTEREST.NOT_FOUND");
        _db.FirmInterestsAL.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<FirmInterestAL> Query(Guid returnId)
        => _db.FirmInterestsAL.Where(f => f.TaxReturnId == returnId
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

    private static FirmInterestAlDto ToDto(FirmInterestAL f) => new(
        f.Id, f.FirmName, f.FirmPan, f.FlatDoorNo, f.Locality, f.City, f.StateCode, f.Pincode, f.Investment);
}
