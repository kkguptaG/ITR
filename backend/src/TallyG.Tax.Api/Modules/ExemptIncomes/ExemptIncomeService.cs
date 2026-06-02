using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.ExemptIncomes;

public interface IExemptIncomeService
{
    Task<IReadOnlyList<ExemptIncomeDto>> ListAsync(Guid returnId, CancellationToken ct = default);
    Task<ExemptIncomeDto> AddAsync(Guid returnId, UpsertExemptIncomeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Exempt-income items (Schedule EI rows for ITR-2/3). Return-scoped, owner/tenant-scoped.
/// Scrutor binds ExemptIncomeService : IExemptIncomeService scoped.
/// </summary>
public sealed class ExemptIncomeService : IExemptIncomeService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ExemptIncomeService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ExemptIncomeDto>> ListAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await Query(returnId).OrderBy(e => e.Category).ThenBy(e => e.Description).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ExemptIncomeDto> AddAsync(Guid returnId, UpsertExemptIncomeRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ExemptIncome
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            Category = r.Category,
            Description = r.Description.Trim(),
            Amount = Clamp(r.Amount),
            District = string.IsNullOrWhiteSpace(r.District) ? null : r.District.Trim(),
            PinCode = string.IsNullOrWhiteSpace(r.PinCode) ? null : r.PinCode.Trim(),
            LandMeasurement = r.LandMeasurement.HasValue ? Math.Round(r.LandMeasurement.Value, 2) : null,
            LandOwned = r.LandOwned,
            LandIrrigated = r.LandIrrigated,
        };
        _db.ExemptIncomes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await Query(returnId).FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw AppException.NotFound("Exempt income item not found.", "EXEMPTINCOME.NOT_FOUND");
        _db.ExemptIncomes.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<ExemptIncome> Query(Guid returnId)
        => _db.ExemptIncomes.Where(e => e.TaxReturnId == returnId
                                        && e.TenantId == _currentUser.TenantId && e.UserId == _currentUser.UserId);

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

    private static ExemptIncomeDto ToDto(ExemptIncome e) => new(
        e.Id, e.Category, e.Description, e.Amount,
        e.District, e.PinCode, e.LandMeasurement, e.LandOwned, e.LandIrrigated);
}
