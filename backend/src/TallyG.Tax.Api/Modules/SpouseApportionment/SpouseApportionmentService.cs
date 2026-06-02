using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.SpouseApportionment;

public interface ISpouseApportionmentService
{
    Task<SpouseApportionmentDto?> GetAsync(Guid returnId, CancellationToken ct = default);
    Task<SpouseApportionmentDto> UpsertAsync(Guid returnId, UpsertSpouseApportionmentRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid returnId, CancellationToken ct = default);
}

/// <summary>
/// Portuguese-Civil-Code spouse apportionment (Schedule 5A), one record per return. Return-scoped,
/// owner/tenant-scoped. Scrutor binds SpouseApportionmentService : ISpouseApportionmentService scoped.
/// </summary>
public sealed class SpouseApportionmentService : ISpouseApportionmentService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public SpouseApportionmentService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SpouseApportionmentDto?> GetAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await Query(returnId).FirstOrDefaultAsync(ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<SpouseApportionmentDto> UpsertAsync(Guid returnId, UpsertSpouseApportionmentRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = await Query(returnId).FirstOrDefaultAsync(ct);
        if (entity is null)
        {
            entity = new SpouseIncomeApportionment { TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id };
            _db.SpouseIncomeApportionments.Add(entity);
        }
        entity.SpouseName = r.SpouseName.Trim();
        entity.SpousePan = r.SpousePan.Trim().ToUpperInvariant();
        entity.SpouseAadhaar = string.IsNullOrWhiteSpace(r.SpouseAadhaar) ? null : r.SpouseAadhaar.Trim();
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await Query(returnId).FirstOrDefaultAsync(ct);
        if (entity is not null)
        {
            _db.SpouseIncomeApportionments.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<SpouseIncomeApportionment> Query(Guid returnId)
        => _db.SpouseIncomeApportionments.Where(s => s.TaxReturnId == returnId
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

    private static SpouseApportionmentDto ToDto(SpouseIncomeApportionment s) => new(s.SpouseName, s.SpousePan, s.SpouseAadhaar);
}
