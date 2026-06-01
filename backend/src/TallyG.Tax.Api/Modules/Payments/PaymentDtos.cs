// Payments module — request/response DTOs.
// These records are the public Payments/Wallet/Coupons contract (docs 04 §4.2, 07 §7.7).
// JSON is camelCase on the wire (ASP.NET Core default), mapping to these PascalCase records.
// Money is decimal in code (NUMERIC(14,2) in the DB) per the global backend rules.

using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Payments;

// ----------------------------------------------------------------- pricing/plans

/// <summary>A purchasable plan/SKU surfaced to the client checkout (GET /pricing/plans).</summary>
public sealed record PlanDto(
    Guid Id,
    string Code,
    string Name,
    decimal Price,
    string BillingPeriod,
    IReadOnlyList<string> Features,
    bool IsActive);

// ----------------------------------------------------------------- create order

/// <summary>
/// POST /payments/orders body. The order is for the filing fee of <see cref="ReturnId"/>,
/// priced from <see cref="PlanCode"/>, with an optional <see cref="CouponCode"/> discount and
/// an optional wallet draw-down. <see cref="Gateway"/> defaults to "razorpay"; "wallet" pays
/// instantly from the wallet balance. Send an <c>Idempotency-Key</c> header to make retries safe.
/// </summary>
public sealed record CreateOrderRequest(
    Guid ReturnId,
    string PlanCode,
    string? CouponCode = null,
    string? Gateway = null,
    bool UseWallet = false);

/// <summary>
/// POST /payments/orders response. Mirrors a gateway "create order" result plus the resolved
/// pricing so the (mock) checkout can render the amount, discount and wallet adjustment.
/// </summary>
public sealed record CreateOrderResponse(
    Guid PaymentId,
    string Gateway,
    string? GatewayOrderId,
    string? GatewayKeyId,
    string Currency,
    decimal BaseAmount,
    decimal DiscountAmount,
    decimal WalletApplied,
    decimal GstAmount,
    decimal AmountPayable,
    string Status,
    bool RequiresGatewayCheckout);

// ----------------------------------------------------------------- verify

/// <summary>POST /payments/{id}:verify body — the signed result returned by the gateway checkout.</summary>
public sealed record VerifyPaymentRequest(string GatewayPaymentId, string Signature);

/// <summary>POST /payments/{id}:verify response.</summary>
public sealed record VerifyPaymentResponse(
    Guid PaymentId,
    string Status,
    Guid? InvoiceId,
    string? InvoiceNumber,
    Guid? TaxReturnId,
    string? ReturnStatus);

// ----------------------------------------------------------------- payment views

/// <summary>List/detail projection of a payment (GET /payments, GET /payments/{id}).</summary>
public sealed record PaymentDto(
    Guid Id,
    Guid? TaxReturnId,
    string Gateway,
    string? GatewayOrderId,
    string? GatewayPaymentId,
    decimal Amount,
    string Currency,
    decimal Gst,
    decimal DiscountAmount,
    decimal WalletApplied,
    string Status,
    Guid? InvoiceId,
    string? InvoiceNumber,
    DateTimeOffset CreatedAt);

// ----------------------------------------------------------------- invoice

/// <summary>GST invoice projection (GET /payments/{id}/invoice).</summary>
public sealed record InvoiceDto(
    Guid Id,
    Guid PaymentId,
    string Number,
    decimal Amount,
    decimal Gst,
    decimal Total,
    string? GstinSeller,
    string? PlaceOfSupply,
    DateTimeOffset IssuedAt);

// ----------------------------------------------------------------- refund

/// <summary>POST /payments/{id}/refund body. Amount omitted = full refund.</summary>
public sealed record RefundRequest(string? Reason = null);

/// <summary>POST /payments/{id}/refund response.</summary>
public sealed record RefundResponse(Guid PaymentId, string Status, decimal RefundedAmount);

// ----------------------------------------------------------------- webhooks

/// <summary>
/// Minimal normalized gateway webhook body (POST /webhooks/razorpay|cashfree). Real gateways post
/// a richer event envelope; the stub verifies the same HMAC scheme over orderId|paymentId.
/// </summary>
public sealed record GatewayWebhookRequest(
    string? Event,
    string OrderId,
    string PaymentId,
    string Signature,
    string? Status = null);

// ----------------------------------------------------------------- wallet

/// <summary>GET /wallet response.</summary>
public sealed record WalletDto(Guid Id, decimal Balance, string Currency, DateTimeOffset UpdatedAt);

/// <summary>A single ledger entry (GET /wallet/transactions).</summary>
public sealed record WalletTransactionDto(
    Guid Id,
    string Type,
    decimal Amount,
    decimal BalanceAfter,
    string? Reference,
    string? Note,
    DateTimeOffset CreatedAt);

/// <summary>
/// POST /wallet:credit body (admin/dev). Adds prepaid credit to a wallet — used for refunds,
/// referral rewards and promo credits. <see cref="UserId"/> defaults to the caller when omitted.
/// </summary>
public sealed record WalletCreditRequest(
    decimal Amount,
    string? Note = null,
    Guid? UserId = null,
    string Type = "credit");

// ----------------------------------------------------------------- coupons

/// <summary>POST /coupons:validate body — check a coupon against a plan's price.</summary>
public sealed record CouponValidateRequest(string Code, string PlanCode);

/// <summary>POST /coupons:apply body — apply a coupon to an existing (unpaid) order.</summary>
public sealed record CouponApplyRequest(string Code, Guid PaymentId);

/// <summary>Coupon pricing result shared by :validate and :apply.</summary>
public sealed record CouponResultDto(
    string Code,
    bool Valid,
    string Type,
    decimal Value,
    decimal BaseAmount,
    decimal DiscountAmount,
    decimal NetAmount,
    string? Message);
