using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.ForeignSourceIncomes;

public interface IForeignSourceIncomeService
{
    Task<IReadOnlyList<ForeignSourceIncomeDto>> ListAsync(Guid returnId, CancellationToken ct = default);
    Task<ForeignSourceIncomeDto> AddAsync(Guid returnId, UpsertForeignSourceIncomeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Foreign-source income + double-taxation relief lines (Schedule FSI / TR1 for ITR-2/3). Return-scoped,
/// owner/tenant-scoped. Scrutor binds ForeignSourceIncomeService : IForeignSourceIncomeService scoped.
/// </summary>
public sealed class ForeignSourceIncomeService : IForeignSourceIncomeService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ForeignSourceIncomeService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ForeignSourceIncomeDto>> ListAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await Query(returnId).OrderBy(f => f.CountryName).ThenBy(f => f.Head).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ForeignSourceIncomeDto> AddAsync(Guid returnId, UpsertForeignSourceIncomeRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new ForeignSourceIncome
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            CountryCode = r.CountryCode.Trim(),
            CountryName = r.CountryName.Trim(),
            TaxIdentificationNo = r.TaxIdentificationNo.Trim(),
            Head = r.Head,
            IncomeFromOutsideIndia = Clamp(r.IncomeFromOutsideIndia),
            TaxPaidOutsideIndia = Clamp(r.TaxPaidOutsideIndia),
            ReliefSection = r.ReliefSection,
            DtaaArticle = string.IsNullOrWhiteSpace(r.DtaaArticle) ? null : r.DtaaArticle.Trim(),
        };
        _db.ForeignSourceIncomes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await Query(returnId).FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw AppException.NotFound("Foreign income line not found.", "FOREIGNSOURCEINCOME.NOT_FOUND");
        _db.ForeignSourceIncomes.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<ForeignSourceIncome> Query(Guid returnId)
        => _db.ForeignSourceIncomes.Where(f => f.TaxReturnId == returnId
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

    private static ForeignSourceIncomeDto ToDto(ForeignSourceIncome f) => new(
        f.Id, f.CountryCode, f.CountryName, f.TaxIdentificationNo, f.Head,
        f.IncomeFromOutsideIndia, f.TaxPaidOutsideIndia, f.ReliefSection, f.DtaaArticle);
}
