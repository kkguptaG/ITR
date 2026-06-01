using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Accounting;

/// <summary>
/// Reads posted movement (total debits / credits and the number of touching vouchers) for a set of
/// ledgers. The join onto <c>Vouchers</c> applies that entity's soft-delete query filter, so entries
/// of deleted vouchers are excluded. Aggregation is done in memory (demo scale) to stay portable
/// across the Postgres and Sqlite providers.
/// </summary>
internal static class LedgerBalances
{
    public readonly record struct Movement(decimal Debits, decimal Credits, int Vouchers);

    public static async Task<IReadOnlyDictionary<Guid, Movement>> ComputeAsync(
        AppDbContext db, Guid tenantId, IReadOnlyCollection<Guid> ledgerIds, CancellationToken ct)
    {
        if (ledgerIds.Count == 0)
        {
            return new Dictionary<Guid, Movement>();
        }

        var rows = await (
            from e in db.VoucherEntries.AsNoTracking()
            join v in db.Vouchers on e.VoucherId equals v.Id
            where e.TenantId == tenantId && ledgerIds.Contains(e.LedgerId)
            select new { e.LedgerId, e.Direction, e.Amount, e.VoucherId }).ToListAsync(ct);

        return rows
            .GroupBy(r => r.LedgerId)
            .ToDictionary(
                g => g.Key,
                g => new Movement(
                    g.Where(x => x.Direction == DrCr.Debit).Sum(x => x.Amount),
                    g.Where(x => x.Direction == DrCr.Credit).Sum(x => x.Amount),
                    g.Select(x => x.VoucherId).Distinct().Count()));
    }
}
