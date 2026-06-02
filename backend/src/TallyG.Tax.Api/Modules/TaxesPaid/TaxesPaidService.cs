using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.TaxesPaid;

public interface ITaxesPaidService
{
    Task<TaxesPaidSummaryDto> GetAsync(Guid returnId, CancellationToken ct = default);
    Task<TdsEntryDto> AddTdsAsync(Guid returnId, UpsertTdsEntryRequest request, CancellationToken ct = default);
    Task DeleteTdsAsync(Guid returnId, Guid id, CancellationToken ct = default);
    Task<ChallanDto> AddChallanAsync(Guid returnId, UpsertChallanRequest request, CancellationToken ct = default);
    Task DeleteChallanAsync(Guid returnId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Deductor-wise TDS + self-paid challans for a return (owner/tenant-scoped). Every mutation rolls the
/// totals up onto the return's prepaid-tax fields (TdsPaid / AdvanceTaxPaid / SelfAssessmentTaxPaid) so
/// the refund/payable math — and the ITR's TaxesPaid summary — reflect the itemised detail. Scrutor
/// binds TaxesPaidService : ITaxesPaidService scoped.
/// </summary>
public sealed class TaxesPaidService : ITaxesPaidService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public TaxesPaidService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TaxesPaidSummaryDto> GetAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var tds = await TdsQuery(returnId).ToListAsync(ct);
        var challans = await ChallanQuery(returnId).ToListAsync(ct);
        return Summarize(tds, challans);
    }

    public async Task<TdsEntryDto> AddTdsAsync(Guid returnId, UpsertTdsEntryRequest request, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new TdsEntry
        {
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            TaxReturnId = ret.Id,
            Head = request.Head,
            DeductorTan = request.DeductorTan.Trim().ToUpperInvariant(),
            DeductorName = request.DeductorName.Trim(),
            TdsSection = string.IsNullOrWhiteSpace(request.TdsSection) ? null : request.TdsSection.Trim().ToUpperInvariant(),
            IncomeOffered = Math.Max(0m, request.IncomeOffered),
            TaxDeducted = Math.Max(0m, request.TaxDeducted),
        };
        _db.TdsEntries.Add(entity);
        await _db.SaveChangesAsync(ct);
        await RecomputeRollupsAsync(ret, ct);
        return ToDto(entity);
    }

    public async Task DeleteTdsAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = await TdsQuery(returnId).FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw AppException.NotFound("TDS entry not found.", "TDS.NOT_FOUND");
        _db.TdsEntries.Remove(entity);
        await _db.SaveChangesAsync(ct);
        await RecomputeRollupsAsync(ret, ct);
    }

    public async Task<ChallanDto> AddChallanAsync(Guid returnId, UpsertChallanRequest request, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new TaxPaymentChallan
        {
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            TaxReturnId = ret.Id,
            Kind = request.Kind,
            BsrCode = request.BsrCode.Trim().ToUpperInvariant(),
            DepositDate = request.DepositDate,
            ChallanSerial = request.ChallanSerial,
            Amount = Math.Max(0m, request.Amount),
        };
        _db.TaxPaymentChallans.Add(entity);
        await _db.SaveChangesAsync(ct);
        await RecomputeRollupsAsync(ret, ct);
        return ToDto(entity);
    }

    public async Task DeleteChallanAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = await ChallanQuery(returnId).FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw AppException.NotFound("Challan not found.", "CHALLAN.NOT_FOUND");
        _db.TaxPaymentChallans.Remove(entity);
        await _db.SaveChangesAsync(ct);
        await RecomputeRollupsAsync(ret, ct);
    }

    // ----------------------------------------------------------------- helpers

    /// <summary>Re-derive the return's prepaid-tax rollups from the itemised rows (the source of truth
    /// once the user itemises). Leaves TCS untouched (captured separately/lump-sum for now).</summary>
    private async Task RecomputeRollupsAsync(TaxReturn ret, CancellationToken ct)
    {
        var tds = await TdsQuery(ret.Id).ToListAsync(ct);
        var challans = await ChallanQuery(ret.Id).ToListAsync(ct);

        ret.TdsPaid = tds.Sum(t => t.TaxDeducted);
        ret.AdvanceTaxPaid = challans.Where(c => c.Kind == ChallanKind.Advance).Sum(c => c.Amount);
        ret.SelfAssessmentTaxPaid = challans.Where(c => c.Kind == ChallanKind.SelfAssessment).Sum(c => c.Amount);
        await _db.SaveChangesAsync(ct);
    }

    private static TaxesPaidSummaryDto Summarize(List<TdsEntry> tds, List<TaxPaymentChallan> challans)
    {
        var salaryTds = tds.Where(t => t.Head == TdsHead.Salary).Sum(t => t.TaxDeducted);
        var otherTds = tds.Where(t => t.Head == TdsHead.OtherThanSalary).Sum(t => t.TaxDeducted);
        var advance = challans.Where(c => c.Kind == ChallanKind.Advance).Sum(c => c.Amount);
        var sat = challans.Where(c => c.Kind == ChallanKind.SelfAssessment).Sum(c => c.Amount);

        return new TaxesPaidSummaryDto(
            tds.OrderBy(t => t.Head).ThenBy(t => t.DeductorName).Select(ToDto).ToList(),
            challans.OrderBy(c => c.Kind).ThenBy(c => c.DepositDate).Select(ToDto).ToList(),
            salaryTds, otherTds, salaryTds + otherTds, advance, sat,
            salaryTds + otherTds + advance + sat);
    }

    private IQueryable<TdsEntry> TdsQuery(Guid returnId)
        => _db.TdsEntries.Where(t => t.TaxReturnId == returnId
                                     && t.TenantId == _currentUser.TenantId && t.UserId == _currentUser.UserId);

    private IQueryable<TaxPaymentChallan> ChallanQuery(Guid returnId)
        => _db.TaxPaymentChallans.Where(c => c.TaxReturnId == returnId
                                             && c.TenantId == _currentUser.TenantId && c.UserId == _currentUser.UserId);

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

    private static TdsEntryDto ToDto(TdsEntry t)
        => new(t.Id, t.Head, t.DeductorTan, t.DeductorName, t.TdsSection, t.IncomeOffered, t.TaxDeducted);

    private static ChallanDto ToDto(TaxPaymentChallan c)
        => new(c.Id, c.Kind, c.BsrCode, c.DepositDate, c.ChallanSerial, c.Amount);
}
