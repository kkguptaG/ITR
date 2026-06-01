using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Payments;

/// <summary>
/// Wallet + ledger implementation. The wallet holds prepaid credits (top-ups, refunds, referral
/// rewards) usable against any fee (Ch.7 §7.7.6). The ledger is append-only and each row snapshots
/// <see cref="WalletTransaction.BalanceAfter"/> so audits are O(1) (Ch.2 §2.7). All access is scoped
/// to the current tenant; credits to another user are gated to Admin/Ops by the controller.
/// </summary>
public sealed class WalletService : IWalletService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;

    public WalletService(AppDbContext db, ICurrentUser currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    // ------------------------------------------------------------------- queries

    public async Task<WalletDto> GetWalletAsync(CancellationToken ct = default)
    {
        var wallet = await GetOrCreateWalletAsync(_currentUser.UserId, ct);
        if (_db.Entry(wallet).State == EntityState.Added)
        {
            await _db.SaveChangesAsync(ct);
        }

        return ToDto(wallet);
    }

    public async Task<PagedResult<WalletTransactionDto>> GetTransactionsAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 100);

        var wallet = await _db.Wallets
            .FirstOrDefaultAsync(w => w.TenantId == _currentUser.TenantId && w.UserId == _currentUser.UserId, ct);

        if (wallet is null)
        {
            return PagedResult<WalletTransactionDto>.Empty(page, pageSize);
        }

        var query = _db.WalletTransactions
            .Where(t => t.WalletId == wallet.Id)
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id);

        var total = await query.LongCountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new WalletTransactionDto(
                t.Id, t.Type.ToString(), t.Amount, t.BalanceAfter, t.Reference, t.Note, t.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<WalletTransactionDto>(items, page, pageSize, total);
    }

    // ------------------------------------------------------------------- credit (api)

    public async Task<WalletDto> CreditAsync(WalletCreditRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
        {
            throw AppException.Validation("Credit amount must be positive.", "PAYMENT.WALLET_AMOUNT");
        }

        var targetUserId = request.UserId ?? _currentUser.UserId;
        var type = ParseCreditType(request.Type);

        var txn = await CreditWalletAsync(
            targetUserId, request.Amount, type, reference: "manual", note: request.Note, save: false, ct);

        await _db.SaveChangesAsync(ct);

        var wallet = await _db.Wallets.FirstAsync(w => w.Id == txn.WalletId, ct);
        return ToDto(wallet);
    }

    // ------------------------------------------------------------------- primitives

    public async Task<WalletTransaction> CreditWalletAsync(
        Guid userId, decimal amount, WalletTransactionType type, string? reference, string? note,
        bool save, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            throw AppException.Validation("Credit amount must be positive.", "PAYMENT.WALLET_AMOUNT");
        }

        var wallet = await GetOrCreateWalletAsync(userId, ct);
        wallet.Balance = PricingMath.Money(wallet.Balance + amount);

        var txn = NewTxn(wallet, type, amount, reference, note);
        _db.WalletTransactions.Add(txn);

        if (save)
        {
            await _db.SaveChangesAsync(ct);
        }

        return txn;
    }

    public async Task<WalletTransaction> DebitWalletAsync(
        Guid userId, decimal amount, string? reference, string? note,
        bool save, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            throw AppException.Validation("Debit amount must be positive.", "PAYMENT.WALLET_AMOUNT");
        }

        var wallet = await GetOrCreateWalletAsync(userId, ct);
        if (wallet.Balance < amount)
        {
            throw new AppException(
                "PAYMENT.WALLET_INSUFFICIENT",
                "Wallet balance is insufficient for this payment.",
                422);
        }

        wallet.Balance = PricingMath.Money(wallet.Balance - amount);

        var txn = NewTxn(wallet, WalletTransactionType.Debit, amount, reference, note);
        _db.WalletTransactions.Add(txn);

        if (save)
        {
            await _db.SaveChangesAsync(ct);
        }

        return txn;
    }

    public async Task<Wallet> GetOrCreateWalletAsync(Guid userId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        // Check the change-tracker first: within a single request the wallet may have been created
        // (but not yet saved) by an earlier call in the same flow. Querying the DB again would miss
        // it and add a duplicate, violating the wallets.user_id unique constraint.
        var tracked = _db.Wallets.Local
            .FirstOrDefault(w => w.TenantId == tenantId && w.UserId == userId);
        if (tracked is not null)
        {
            return tracked;
        }

        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.TenantId == tenantId && w.UserId == userId, ct);
        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new Wallet
        {
            TenantId = tenantId,
            UserId = userId,
            Balance = 0m,
            Currency = "INR"
        };
        _db.Wallets.Add(wallet);
        return wallet;
    }

    // ------------------------------------------------------------------- internals

    private WalletTransaction NewTxn(
        Wallet wallet, WalletTransactionType type, decimal amount, string? reference, string? note) => new()
    {
        TenantId = wallet.TenantId,
        WalletId = wallet.Id,
        Wallet = wallet,
        Type = type,
        Amount = PricingMath.Money(amount),
        BalanceAfter = wallet.Balance,
        Reference = reference,
        Note = note,
        CreatedAt = _clock.UtcNow
    };

    private static WalletDto ToDto(Wallet w) => new(w.Id, w.Balance, w.Currency, w.UpdatedAt);

    private static WalletTransactionType ParseCreditType(string? type) => (type ?? "credit").Trim().ToLowerInvariant() switch
    {
        "credit" => WalletTransactionType.Credit,
        "refund" => WalletTransactionType.Refund,
        "referralbonus" or "referral_bonus" or "referral" => WalletTransactionType.ReferralBonus,
        "cashback" => WalletTransactionType.Cashback,
        _ => throw AppException.Validation($"Unsupported wallet credit type '{type}'.", "PAYMENT.WALLET_TYPE")
    };
}
