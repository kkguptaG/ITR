using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Payments;

/// <summary>
/// Filing-fee payments: order creation (idempotent, coupon + wallet aware), client-signature
/// verification, GST invoice issuance, refunds, and gateway webhook handling. Implements the
/// `pay -> file` money path (Ch.1, Ch.4 §4.2, Ch.7 §7.7). Auto-registered scoped by Scrutor
/// (PaymentService : IPaymentService).
/// </summary>
public interface IPaymentService
{
    /// <summary>GET /pricing/plans — active plans/SKUs for the tenant.</summary>
    Task<IReadOnlyList<PlanDto>> GetPlansAsync(CancellationToken ct = default);

    /// <summary>
    /// POST /payments/orders — create a payment order for a return's filing fee. Honours the
    /// <c>Idempotency-Key</c> (replays return the original response). Applies coupon + wallet.
    /// A wallet-only payment is captured immediately; a gateway payment returns an order token.
    /// </summary>
    Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest request, string? idempotencyKey, CancellationToken ct = default);

    /// <summary>POST /payments/{id}:verify — verify the gateway signature, capture, invoice, mark return Paid.</summary>
    Task<VerifyPaymentResponse> VerifyAsync(Guid paymentId, VerifyPaymentRequest request, CancellationToken ct = default);

    /// <summary>POST /payments/{id}/refund — refund a captured payment (credits the wallet). Ops/Admin.</summary>
    Task<RefundResponse> RefundAsync(Guid paymentId, RefundRequest request, CancellationToken ct = default);

    /// <summary>POST /webhooks/{gateway} — idempotent, signature-verified status update from the PSP.</summary>
    Task HandleWebhookAsync(string gateway, GatewayWebhookRequest request, CancellationToken ct = default);

    /// <summary>GET /payments — the caller's payments (paged, newest first).</summary>
    Task<PagedResult<PaymentDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>GET /payments/{id} — one of the caller's payments.</summary>
    Task<PaymentDto> GetAsync(Guid paymentId, CancellationToken ct = default);

    /// <summary>GET /payments/{id}/invoice — the GST invoice for a captured payment.</summary>
    Task<InvoiceDto> GetInvoiceAsync(Guid paymentId, CancellationToken ct = default);
}
