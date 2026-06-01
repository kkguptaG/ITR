using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Payments;

/// <summary>
/// Wallet balance + append-only ledger for the current user, plus the credit/debit primitives the
/// payment flow uses (wallet-as-gateway debit, refund-to-wallet credit). Auto-registered scoped by
/// Scrutor (WalletService : IWalletService).
/// </summary>
public interface IWalletService
{
    /// <summary>Current user's wallet (created on first access).</summary>
    Task<WalletDto> GetWalletAsync(CancellationToken ct = default);

    /// <summary>Current user's ledger, newest first.</summary>
    Task<PagedResult<WalletTransactionDto>> GetTransactionsAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>Admin/dev credit endpoint (POST /wallet:credit).</summary>
    Task<WalletDto> CreditAsync(WalletCreditRequest request, CancellationToken ct = default);

    /// <summary>
    /// Add funds to a user's wallet within the caller's tenant. Used by refunds and admin credits.
    /// Writes a ledger entry and returns it. Does not call SaveChanges unless <paramref name="save"/>.
    /// </summary>
    Task<WalletTransaction> CreditWalletAsync(
        Guid userId, decimal amount, WalletTransactionType type, string? reference, string? note,
        bool save, CancellationToken ct = default);

    /// <summary>
    /// Debit a user's wallet, enforcing a non-negative balance. Throws <see cref="AppException"/>
    /// (PAYMENT.WALLET_INSUFFICIENT) when the balance is too low. Used by wallet-as-gateway payment.
    /// Does not call SaveChanges unless <paramref name="save"/>.
    /// </summary>
    Task<WalletTransaction> DebitWalletAsync(
        Guid userId, decimal amount, string? reference, string? note,
        bool save, CancellationToken ct = default);

    /// <summary>Load-or-create the wallet row for a user (no save).</summary>
    Task<Wallet> GetOrCreateWalletAsync(Guid userId, CancellationToken ct = default);
}
