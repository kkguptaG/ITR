using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Donations;

public interface IDonation80GService
{
    Task<IReadOnlyList<Donation80GDto>> ListAsync(Guid returnId, CancellationToken ct = default);
    Task<Donation80GDto> AddAsync(Guid returnId, UpsertDonation80GRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Itemised 80G donations (donee-wise rows for Schedule 80G). Return-scoped, owner/tenant-scoped.
/// Scrutor binds Donation80GService : IDonation80GService scoped.
/// </summary>
public sealed class Donation80GService : IDonation80GService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public Donation80GService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<Donation80GDto>> ListAsync(Guid returnId, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var rows = await Query(returnId).OrderBy(d => d.Category).ThenBy(d => d.DoneeName).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<Donation80GDto> AddAsync(Guid returnId, UpsertDonation80GRequest r, CancellationToken ct = default)
    {
        var ret = await OwnedReturnAsync(returnId, ct);
        var entity = new Donation80G
        {
            TenantId = ret.TenantId, UserId = ret.UserId, TaxReturnId = ret.Id,
            DoneeName = r.DoneeName.Trim(),
            DoneePan = r.DoneePan.Trim().ToUpperInvariant(),
            ArnNumber = string.IsNullOrWhiteSpace(r.ArnNumber) ? null : r.ArnNumber.Trim(),
            AddressLine = r.AddressLine.Trim(),
            City = r.City.Trim(),
            StateCode = r.StateCode.Trim(),
            Pincode = r.Pincode.Trim(),
            Category = r.Category,
            CashAmount = Clamp(r.CashAmount),
            OtherModeAmount = Clamp(r.OtherModeAmount),
        };
        _db.Donations80G.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid returnId, Guid id, CancellationToken ct = default)
    {
        await EnsureOwnedReturnAsync(returnId, ct);
        var entity = await Query(returnId).FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw AppException.NotFound("Donation not found.", "DONATION80G.NOT_FOUND");
        _db.Donations80G.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers
    private IQueryable<Donation80G> Query(Guid returnId)
        => _db.Donations80G.Where(d => d.TaxReturnId == returnId
                                       && d.TenantId == _currentUser.TenantId && d.UserId == _currentUser.UserId);

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

    /// <summary>Deduction factor: 100% for the "HundredPercent" buckets, 50% for the "FiftyPercent" buckets.</summary>
    private static decimal Factor(Donation80GCategory c)
        => c is Donation80GCategory.HundredPercentNoLimit or Donation80GCategory.HundredPercentWithLimit ? 1.0m : 0.5m;

    private static decimal Eligible(Donation80G d)
    {
        // A cash donation over ₹2,000 is wholly disallowed; the non-cash part is always eligible.
        var eligibleBase = d.OtherModeAmount + (d.CashAmount <= 2_000m ? d.CashAmount : 0m);
        return Math.Round(eligibleBase * Factor(d.Category), MidpointRounding.AwayFromZero);
    }

    private static Donation80GDto ToDto(Donation80G d) => new(
        d.Id, d.DoneeName, d.DoneePan, d.ArnNumber, d.AddressLine, d.City, d.StateCode, d.Pincode,
        d.Category, d.CashAmount, d.OtherModeAmount, d.CashAmount + d.OtherModeAmount, Eligible(d));
}
