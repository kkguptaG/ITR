using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Infrastructure.Persistence;
using Entity = TallyG.Tax.Domain.Entities.AssetsLiabilities;

namespace TallyG.Tax.Api.Modules.AssetsLiabilities;

public interface IAssetsLiabilitiesService
{
    Task<AssetsLiabilitiesDto> GetAsync(Guid returnId, CancellationToken ct = default);
    Task<AssetsLiabilitiesDto> UpsertAsync(Guid returnId, UpsertAssetsLiabilitiesRequest request, CancellationToken ct = default);
}

/// <summary>
/// The return's Schedule AL declaration (movable assets + liabilities). One row per return, upserted.
/// Owner/tenant-scoped. Scrutor binds AssetsLiabilitiesService : IAssetsLiabilitiesService scoped.
/// </summary>
public sealed class AssetsLiabilitiesService : IAssetsLiabilitiesService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AssetsLiabilitiesService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AssetsLiabilitiesDto> GetAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var e = await FindAsync(returnId, ct);
        return e is null ? Empty : ToDto(e);
    }

    public async Task<AssetsLiabilitiesDto> UpsertAsync(Guid returnId, UpsertAssetsLiabilitiesRequest r, CancellationToken ct = default)
    {
        var ret = await _db.TaxReturns.FirstOrDefaultAsync(
                      t => t.Id == returnId && t.TenantId == _currentUser.TenantId && t.UserId == _currentUser.UserId, ct)
                  ?? throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");

        var e = await FindAsync(returnId, ct);
        if (e is null)
        {
            e = new Entity { TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id };
            _db.AssetsLiabilities.Add(e);
        }

        e.BankDeposits = Clamp(r.BankDeposits);
        e.SharesAndSecurities = Clamp(r.SharesAndSecurities);
        e.InsurancePolicies = Clamp(r.InsurancePolicies);
        e.LoansAndAdvancesGiven = Clamp(r.LoansAndAdvancesGiven);
        e.CashInHand = Clamp(r.CashInHand);
        e.JewelleryBullion = Clamp(r.JewelleryBullion);
        e.ArtCollections = Clamp(r.ArtCollections);
        e.Vehicles = Clamp(r.Vehicles);
        e.Liabilities = Clamp(r.Liabilities);

        await _db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    // ----------------------------------------------------------------- helpers
    private Task<Entity?> FindAsync(Guid returnId, CancellationToken ct)
        => _db.AssetsLiabilities.FirstOrDefaultAsync(
            a => a.TaxReturnId == returnId && a.TenantId == _currentUser.TenantId && a.UserId == _currentUser.UserId, ct);

    private async Task EnsureOwnedReturnAsync(Guid returnId, CancellationToken ct)
    {
        if (!await _db.TaxReturns.AnyAsync(
                t => t.Id == returnId && t.TenantId == _currentUser.TenantId && t.UserId == _currentUser.UserId, ct))
        {
            throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");
        }
    }

    private static decimal Clamp(decimal v) => Math.Clamp(v, 0m, 99_999_999_999_999m);

    private static readonly AssetsLiabilitiesDto Empty = new(0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static AssetsLiabilitiesDto ToDto(Entity e) => new(
        e.BankDeposits, e.SharesAndSecurities, e.InsurancePolicies, e.LoansAndAdvancesGiven,
        e.CashInHand, e.JewelleryBullion, e.ArtCollections, e.Vehicles, e.Liabilities);
}
