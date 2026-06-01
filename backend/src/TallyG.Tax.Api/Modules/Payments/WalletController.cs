using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Payments;

/// <summary>
/// Wallet balance, ledger and credit (docs 04 §4.2). Reads/credit-self are available to the
/// authenticated user; crediting another user is gated to Ops/Admin. The wallet is also usable as a
/// payment source (Gateway=Wallet) via <see cref="PaymentsController"/>.
/// </summary>
[ApiController]
[Route("api/v1/wallet")]
[Authorize]
public sealed class WalletController : ControllerBase
{
    private readonly IWalletService _wallet;

    public WalletController(IWalletService wallet) => _wallet = wallet;

    /// <summary>Current user's wallet balance.</summary>
    [HttpGet("")]
    [ProducesResponseType(typeof(WalletDto), StatusCodes.Status200OK)]
    public Task<WalletDto> Get(CancellationToken ct) => _wallet.GetWalletAsync(ct);

    /// <summary>Current user's wallet ledger (newest first).</summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(PagedResult<WalletTransactionDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<WalletTransactionDto>> Transactions(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _wallet.GetTransactionsAsync(page, pageSize, ct);

    /// <summary>
    /// Credit a wallet (admin/dev). Crediting your own wallet is allowed for the dev demo; crediting
    /// another user's wallet requires Ops/Admin. Used for refunds, referral rewards and promo credits.
    /// </summary>
    [HttpPost(":credit")]
    [ProducesResponseType(typeof(WalletDto), StatusCodes.Status200OK)]
    public Task<WalletDto> Credit([FromBody] WalletCreditRequest request, CancellationToken ct)
    {
        // Crediting someone else's wallet is privileged; crediting your own is self-service (dev).
        if (request.UserId is { } target && target != _currentUserId() && !IsPrivileged())
        {
            throw AppException.Forbidden("Only Ops/Admin may credit another user's wallet.", "PAYMENT.WALLET_FORBIDDEN");
        }

        return _wallet.CreditAsync(request, ct);
    }

    private Guid _currentUserId()
    {
        var sub = User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private bool IsPrivileged() =>
        User.IsInRole("Ops") || User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
}
