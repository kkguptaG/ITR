using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Accounting;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Accounting;

/// <summary>
/// Chart-of-accounts service. All reads/writes are scoped to the caller's tenant + user (their own
/// books). Ledger names are unique per user (case-insensitive, ignoring the " (E)" mark) so the
/// matcher and manual entry converge on one head per account. No manual DI — Scrutor binds
/// LedgerService : ILedgerService.
/// </summary>
public sealed class LedgerService : ILedgerService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly ILogger<LedgerService> _logger;

    public LedgerService(AppDbContext db, ICurrentUser currentUser, IDateTime clock, ILogger<LedgerService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LedgerDto>> ListAsync(
        string? group, bool? systemGeneratedOnly, bool? bankOnly, CancellationToken ct = default)
    {
        RequireAuthenticated();

        var q = _db.Ledgers.AsNoTracking()
            .Where(l => l.TenantId == _currentUser.TenantId && l.UserId == _currentUser.UserId);

        if (!string.IsNullOrWhiteSpace(group) && Enum.TryParse<LedgerGroup>(group, true, out var g))
        {
            q = q.Where(l => l.Group == g);
        }

        if (systemGeneratedOnly == true)
        {
            q = q.Where(l => l.IsSystemGenerated);
        }

        if (bankOnly == true)
        {
            q = q.Where(l => l.IsBank);
        }

        var ledgers = await q.OrderBy(l => l.Group).ThenBy(l => l.Name).ToListAsync(ct);
        var movements = await LedgerBalances.ComputeAsync(
            _db, _currentUser.TenantId, ledgers.Select(l => l.Id).ToList(), ct);

        return ledgers.Select(l => Project(l, movements)).ToList();
    }

    public async Task<LedgerDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var ledger = await LoadOwnedAsync(id, ct);
        var movements = await LedgerBalances.ComputeAsync(_db, _currentUser.TenantId, new[] { ledger.Id }, ct);
        return Project(ledger, movements);
    }

    public async Task<LedgerDto> CreateAsync(CreateLedgerRequest request, CancellationToken ct = default)
    {
        RequireAuthenticated();

        var name = CleanName(request.Name);
        await EnsureNameAvailableAsync(name, excludeId: null, ct);

        var ledger = new Ledger
        {
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            Name = name,
            Group = request.Group,
            Nature = LedgerGroupMeta.NatureOf(request.Group),
            OpeningBalance = decimal.Round(request.OpeningBalance, 2),
            IsBank = request.IsBank || request.Group == LedgerGroup.BankAccounts,
            IsSystemGenerated = false,
            Notes = request.Notes?.Trim()
        };

        _db.Ledgers.Add(ledger);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created ledger {LedgerId} '{Name}' ({Group})", ledger.Id, ledger.Name, ledger.Group);
        return Project(ledger, EmptyMovements);
    }

    public async Task<LedgerDto> UpdateAsync(Guid id, UpdateLedgerRequest request, CancellationToken ct = default)
    {
        var ledger = await LoadOwnedAsync(id, ct);

        var name = CleanName(request.Name);
        await EnsureNameAvailableAsync(name, excludeId: ledger.Id, ct);

        ledger.Name = name;
        ledger.Group = request.Group;
        ledger.Nature = LedgerGroupMeta.NatureOf(request.Group);
        ledger.OpeningBalance = decimal.Round(request.OpeningBalance, 2);
        ledger.IsBank = ledger.IsBank || request.Group == LedgerGroup.BankAccounts;
        ledger.Notes = request.Notes?.Trim();

        // Editing a head is the user adopting it — it is no longer a machine-generated proposal.
        ledger.IsSystemGenerated = false;

        await _db.SaveChangesAsync(ct);

        var movements = await LedgerBalances.ComputeAsync(_db, _currentUser.TenantId, new[] { ledger.Id }, ct);
        _logger.LogInformation("Updated ledger {LedgerId} '{Name}'", ledger.Id, ledger.Name);
        return Project(ledger, movements);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var ledger = await LoadOwnedAsync(id, ct);

        var usedByVoucher = await _db.VoucherEntries
            .AnyAsync(e => e.TenantId == _currentUser.TenantId && e.LedgerId == ledger.Id, ct);
        if (usedByVoucher)
        {
            throw AppException.Conflict(
                "This ledger has posted vouchers and cannot be deleted. Reassign or delete those entries first.",
                "LEDGER.IN_USE");
        }

        var usedByImport = await _db.BankStatementImports
            .AnyAsync(i => i.TenantId == _currentUser.TenantId && i.BankLedgerId == ledger.Id, ct);
        if (usedByImport)
        {
            throw AppException.Conflict(
                "This bank ledger backs an imported statement and cannot be deleted.", "LEDGER.IN_USE");
        }

        ledger.DeletedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Soft-deleted ledger {LedgerId} '{Name}'", ledger.Id, ledger.Name);
    }

    // --------------------------------------------------------------------- helpers

    private static readonly IReadOnlyDictionary<Guid, LedgerBalances.Movement> EmptyMovements
        = new Dictionary<Guid, LedgerBalances.Movement>();

    private static LedgerDto Project(Ledger l, IReadOnlyDictionary<Guid, LedgerBalances.Movement> movements)
    {
        var m = movements.TryGetValue(l.Id, out var mv) ? mv : default;
        return AccountingMappers.ToDto(l, m.Debits, m.Credits, m.Vouchers);
    }

    private async Task<Ledger> LoadOwnedAsync(Guid id, CancellationToken ct)
    {
        RequireAuthenticated();

        var ledger = await _db.Ledgers.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw AppException.NotFound("Ledger not found.", "LEDGER.NOT_FOUND");

        if (ledger.TenantId != _currentUser.TenantId || ledger.UserId != _currentUser.UserId)
        {
            throw AppException.NotFound("Ledger not found.", "LEDGER.NOT_FOUND");
        }

        return ledger;
    }

    private async Task EnsureNameAvailableAsync(string name, Guid? excludeId, CancellationToken ct)
    {
        var stripped = LedgerNaming.Strip(name).ToLowerInvariant();
        var clash = await _db.Ledgers
            .Where(l => l.TenantId == _currentUser.TenantId && l.UserId == _currentUser.UserId)
            .Where(l => excludeId == null || l.Id != excludeId)
            .Select(l => l.Name)
            .ToListAsync(ct);

        if (clash.Any(existing => LedgerNaming.Strip(existing).ToLowerInvariant() == stripped))
        {
            throw AppException.Conflict($"A ledger named '{LedgerNaming.Strip(name)}' already exists.", "LEDGER.DUPLICATE_NAME");
        }
    }

    private static string CleanName(string? name)
    {
        var n = (name ?? string.Empty).Trim();
        if (n.Length == 0)
        {
            throw AppException.Validation("A ledger name is required.", "LEDGER.NAME_REQUIRED");
        }

        return n.Length > 120 ? n[..120] : n;
    }

    private void RequireAuthenticated()
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new AppException("AUTH.UNAUTHENTICATED", "Not authenticated.", 401);
        }
    }
}
