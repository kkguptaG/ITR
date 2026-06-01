using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Payments;

/// <summary>
/// Filing-fee payment orchestration. Prices a return's fee from a plan, applies a coupon and an
/// optional wallet draw-down, then either captures instantly (wallet-only) or hands a gateway order
/// to the client. Verification checks the gateway signature, captures, issues a gapless GST invoice,
/// redeems the coupon, and advances the linked return to <see cref="ReturnStatus.Paid"/>. All gateway
/// calls are routed through the stubbed <see cref="IPaymentGateway"/> so the flow runs without a PSP.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly IPaymentGateway _gateway;
    private readonly IWalletService _wallet;
    private readonly ICouponService _coupons;
    private readonly IInvoiceNumberService _invoiceNumbers;
    private readonly INotificationSender _notifications;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        AppDbContext db,
        ICurrentUser currentUser,
        IDateTime clock,
        IPaymentGateway gateway,
        IWalletService wallet,
        ICouponService coupons,
        IInvoiceNumberService invoiceNumbers,
        INotificationSender notifications,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _gateway = gateway;
        _wallet = wallet;
        _coupons = coupons;
        _invoiceNumbers = invoiceNumbers;
        _notifications = notifications;
        _logger = logger;
    }

    // ============================================================== pricing/plans

    public async Task<IReadOnlyList<PlanDto>> GetPlansAsync(CancellationToken ct = default)
    {
        // Order on the client: Sqlite (no-infra demo path) cannot translate ORDER BY on a
        // decimal column, and the plan list is tiny — so sort after materialization. Works
        // identically on Postgres.
        var plans = await _db.Plans
            .Where(p => p.IsActive)
            .ToListAsync(ct);

        return plans
            .OrderBy(p => p.Price)
            .Select(p => new PlanDto(
                p.Id, p.Code, p.Name, p.Price, p.BillingPeriod, ParseFeatures(p.Features), p.IsActive))
            .ToList();
    }

    // ============================================================== create order

    public async Task<CreateOrderResponse> CreateOrderAsync(
        CreateOrderRequest request, string? idempotencyKey, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.UserId;

        // --- Idempotency: a replay with the same key returns the original order. ---
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.Payments.FirstOrDefaultAsync(
                p => p.TenantId == tenantId && p.IdempotencyKey == idempotencyKey, ct);

            if (existing is not null)
            {
                // Same key must mean the same request (docs 04 §4.1: differing body → 422).
                if (existing.TaxReturnId != request.ReturnId)
                {
                    throw new AppException(
                        "IDEMPOTENCY.KEY_REUSED",
                        "Idempotency-Key was reused with a different request body.",
                        422);
                }

                return await BuildOrderResponseAsync(existing, ct);
            }
        }

        // --- Resolve + guard the target return (ownership + state). ---
        var taxReturn = await _db.TaxReturns.FirstOrDefaultAsync(
            r => r.Id == request.ReturnId && r.TenantId == tenantId && r.UserId == userId, ct)
            ?? throw AppException.NotFound("Tax return not found.", "PAYMENT.RETURN_NOT_FOUND");

        if (taxReturn.Status is ReturnStatus.Paid or ReturnStatus.Filed or ReturnStatus.Processed)
        {
            throw AppException.Conflict(
                "This return has already been paid for.", "PAYMENT.RETURN_ALREADY_PAID");
        }

        // --- Price the fee: plan -> coupon discount -> wallet draw-down -> GST. ---
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Code == request.PlanCode.Trim() && p.IsActive, ct)
            ?? throw AppException.NotFound($"Plan '{request.PlanCode}' was not found.", "PAYMENT.PLAN_NOT_FOUND");

        var baseAmount = PricingMath.Money(plan.Price);

        Coupon? coupon = null;
        var discount = 0m;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            coupon = await _coupons.ResolveAsync(request.CouponCode!, ct);
            discount = PricingMath.ComputeDiscount(coupon, baseAmount, _clock.UtcNow);
        }

        var netFee = PricingMath.Money(baseAmount - discount);
        var gst = PricingMath.Gst(netFee);
        var grossPayable = PricingMath.Money(netFee + gst);

        var gateway = ParseGateway(request.Gateway, request.UseWallet);

        // Wallet draw-down: full balance up to the gross, or the entire amount when paying by wallet.
        var walletApplied = 0m;
        if (gateway == Gateway.Wallet || request.UseWallet)
        {
            var walletEntity = await _wallet.GetOrCreateWalletAsync(userId, ct);
            var balance = walletEntity.Balance;
            walletApplied = gateway == Gateway.Wallet
                ? grossPayable                       // wallet-only: must cover the whole amount
                : Math.Min(balance, grossPayable);   // partial draw-down alongside a gateway

            if (gateway == Gateway.Wallet && balance < grossPayable)
            {
                throw new AppException(
                    "PAYMENT.WALLET_INSUFFICIENT",
                    "Wallet balance is insufficient to cover this order.",
                    422);
            }
        }

        var amountAfterWallet = PricingMath.Money(grossPayable - walletApplied);

        var payment = new Payment
        {
            TenantId = tenantId,
            UserId = userId,
            TaxReturnId = taxReturn.Id,
            Gateway = gateway,
            Amount = grossPayable,
            Currency = "INR",
            TaxGst = gst,
            DiscountAmount = discount,
            WalletApplied = walletApplied,
            CouponId = coupon?.Id,
            Status = PaymentStatus.Created,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey,
            CreatedAt = _clock.UtcNow
        };

        // --- Wallet-only orders capture immediately; gateway orders get an order token. ---
        if (gateway == Gateway.Wallet)
        {
            await _wallet.DebitWalletAsync(
                userId, grossPayable, reference: $"payment:{payment.Id}",
                note: $"Filing fee — {plan.Name}", save: false, ct);

            payment.GatewayOrderId = $"wallet_{payment.Id:N}"[..20];
            payment.GatewayPaymentId = payment.GatewayOrderId;
            payment.Status = PaymentStatus.Paid;

            _db.Payments.Add(payment);
            await CaptureSideEffectsAsync(payment, taxReturn, coupon, ct);
            await _db.SaveChangesAsync(ct);

            return await BuildOrderResponseAsync(payment, ct);
        }

        // STUB: create a gateway order (Razorpay-style). The single registered IPaymentGateway is
        // reused for Cashfree too — the deterministic order id + HMAC scheme is gateway-neutral here.
        var receipt = $"rtn_{taxReturn.Id:N}";
        var order = await _gateway.CreateOrderAsync(amountAfterWallet, payment.Currency, receipt, ct);

        payment.GatewayOrderId = order.OrderId;
        payment.Status = PaymentStatus.Pending;

        _db.Payments.Add(payment);

        // Move the return into the awaiting-payment state.
        if (taxReturn.Status != ReturnStatus.PendingPayment)
        {
            taxReturn.Status = ReturnStatus.PendingPayment;
        }

        await _db.SaveChangesAsync(ct);

        return await BuildOrderResponseAsync(payment, ct, order.KeyId, amountAfterWallet);
    }

    // ============================================================== verify

    public async Task<VerifyPaymentResponse> VerifyAsync(
        Guid paymentId, VerifyPaymentRequest request, CancellationToken ct = default)
    {
        var payment = await LoadOwnedPaymentAsync(paymentId, ct);

        if (payment.Status == PaymentStatus.Paid)
        {
            // Idempotent: re-verifying a captured payment returns the existing result.
            return await BuildVerifyResponseAsync(payment, ct);
        }

        if (payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Created)
        {
            throw AppException.Conflict("This payment cannot be verified in its current state.", "PAYMENT.NOT_PENDING");
        }

        if (string.IsNullOrWhiteSpace(payment.GatewayOrderId))
        {
            throw AppException.Validation("Payment has no gateway order to verify.", "PAYMENT.NO_ORDER");
        }

        // STUB: verify the gateway signature (real HMAC-SHA256 of "orderId|paymentId").
        var ok = _gateway.VerifySignature(payment.GatewayOrderId!, request.GatewayPaymentId, request.Signature);
        if (!ok)
        {
            payment.Status = PaymentStatus.Failed;
            await _db.SaveChangesAsync(ct);
            throw new AppException("PAYMENT.SIGNATURE_INVALID", "Payment signature verification failed.", 422);
        }

        var taxReturn = await LoadReturnAsync(payment.TaxReturnId, ct);
        var coupon = payment.CouponId is { } cid
            ? await _db.Coupons.FirstOrDefaultAsync(c => c.Id == cid, ct)
            : null;

        // Apply any wallet draw-down recorded at order time, now that the gateway leg succeeded.
        if (payment.WalletApplied > 0)
        {
            await _wallet.DebitWalletAsync(
                payment.UserId, payment.WalletApplied, reference: $"payment:{payment.Id}",
                note: "Wallet applied to filing fee", save: false, ct);
        }

        payment.GatewayPaymentId = request.GatewayPaymentId;
        payment.Status = PaymentStatus.Paid;

        await CaptureSideEffectsAsync(payment, taxReturn, coupon, ct);
        await _db.SaveChangesAsync(ct);

        return await BuildVerifyResponseAsync(payment, ct);
    }

    // ============================================================== refund

    public async Task<RefundResponse> RefundAsync(Guid paymentId, RefundRequest request, CancellationToken ct = default)
    {
        // Refund is an Ops/Admin action; it is NOT scoped to the caller's own user id.
        var payment = await _db.Payments.FirstOrDefaultAsync(
            p => p.Id == paymentId && p.TenantId == _currentUser.TenantId, ct)
            ?? throw AppException.NotFound("Payment not found.", "PAYMENT.NOT_FOUND");

        if (payment.Status == PaymentStatus.Refunded)
        {
            return new RefundResponse(payment.Id, payment.Status.ToString(), payment.Amount);
        }

        if (payment.Status != PaymentStatus.Paid)
        {
            throw AppException.Conflict("Only a captured payment can be refunded.", "PAYMENT.NOT_CAPTURED");
        }

        // STUB: a real gateway refund call would go here; we mark refunded and credit the wallet so
        // the money path stays whole (the `pay -> file` saga compensates a paid-but-unfiled return).
        await _wallet.CreditWalletAsync(
            payment.UserId, payment.Amount, WalletTransactionType.Refund,
            reference: $"payment:{payment.Id}",
            note: request.Reason ?? "Payment refunded to wallet", save: false, ct);

        payment.Status = PaymentStatus.Refunded;

        // Roll the linked return back so it can be re-paid (unless it was already filed).
        var taxReturn = await LoadReturnAsync(payment.TaxReturnId, ct);
        if (taxReturn is not null && taxReturn.Status == ReturnStatus.Paid)
        {
            taxReturn.Status = ReturnStatus.PendingPayment;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Refunded payment {PaymentId} ({Amount} {Currency})", payment.Id, payment.Amount, payment.Currency);

        return new RefundResponse(payment.Id, payment.Status.ToString(), payment.Amount);
    }

    // ============================================================== webhook

    public async Task HandleWebhookAsync(string gateway, GatewayWebhookRequest request, CancellationToken ct = default)
    {
        // Webhooks carry no user token; authenticity is the gateway HMAC over orderId|paymentId.
        // STUB: verify with the same scheme used at checkout.
        var ok = _gateway.VerifySignature(request.OrderId, request.PaymentId, request.Signature);
        if (!ok)
        {
            throw new AppException("PAYMENT.SIGNATURE_INVALID", "Webhook signature verification failed.", 422);
        }

        var payment = await _db.Payments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.GatewayOrderId == request.OrderId, ct);

        if (payment is null)
        {
            // Unknown order: ack without action (do not 500 — the PSP retries on non-2xx).
            _logger.LogWarning("Webhook for unknown gateway order {OrderId} ignored.", request.OrderId);
            return;
        }

        // Idempotent: a duplicate "captured" webhook for an already-paid order is a no-op.
        if (payment.Status == PaymentStatus.Paid || payment.Status == PaymentStatus.Refunded)
        {
            return;
        }

        var status = NormalizeWebhookStatus(request.Event, request.Status);

        // Persist the last verified webhook payload (jsonb on Postgres / text on Sqlite).
        payment.WebhookPayloadJson = JsonSerializer.Serialize(request);

        if (status == PaymentStatus.Paid)
        {
            var taxReturn = await LoadReturnAsync(payment.TaxReturnId, ct);
            var coupon = payment.CouponId is { } cid
                ? await _db.Coupons.FirstOrDefaultAsync(c => c.Id == cid, ct)
                : null;

            if (payment.WalletApplied > 0)
            {
                await _wallet.DebitWalletAsync(
                    payment.UserId, payment.WalletApplied, reference: $"payment:{payment.Id}",
                    note: "Wallet applied to filing fee", save: false, ct);
            }

            payment.GatewayPaymentId ??= request.PaymentId;
            payment.Status = PaymentStatus.Paid;
            await CaptureSideEffectsAsync(payment, taxReturn, coupon, ct);
        }
        else if (status == PaymentStatus.Failed)
        {
            payment.Status = PaymentStatus.Failed;
        }

        await _db.SaveChangesAsync(ct);
    }

    // ============================================================== reads

    public async Task<PagedResult<PaymentDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Payments
            .Where(p => p.TenantId == _currentUser.TenantId && p.UserId == _currentUser.UserId)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id);

        var total = await query.LongCountAsync(ct);
        var payments = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = await ProjectAsync(payments, ct);
        return new PagedResult<PaymentDto>(items, page, pageSize, total);
    }

    public async Task<PaymentDto> GetAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payment = await LoadOwnedPaymentAsync(paymentId, ct);
        var items = await ProjectAsync(new[] { payment }, ct);
        return items[0];
    }

    public async Task<InvoiceDto> GetInvoiceAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payment = await LoadOwnedPaymentAsync(paymentId, ct);

        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.PaymentId == payment.Id, ct)
            ?? throw AppException.NotFound("No invoice exists for this payment yet.", "PAYMENT.INVOICE_NOT_FOUND");

        return new InvoiceDto(
            invoice.Id, invoice.PaymentId, invoice.Number, invoice.Amount, invoice.Gst,
            PricingMath.Money(invoice.Amount + invoice.Gst),
            invoice.GstinSeller, invoice.PlaceOfSupply, invoice.IssuedAt);
    }

    // ============================================================== internals

    /// <summary>
    /// Side-effects of a successful capture: redeem the coupon (once), advance the return to Paid,
    /// generate the GST invoice, and "send" a receipt. Mutates entities; the caller saves.
    /// </summary>
    private async Task CaptureSideEffectsAsync(Payment payment, TaxReturn? taxReturn, Coupon? coupon, CancellationToken ct)
    {
        if (coupon is not null)
        {
            coupon.Redeemed += 1;
        }

        if (taxReturn is not null && taxReturn.Status != ReturnStatus.Filed && taxReturn.Status != ReturnStatus.Processed)
        {
            taxReturn.Status = ReturnStatus.Paid;
        }

        await GenerateInvoiceAsync(payment, ct);

        // STUB: notify the user. The console sender just logs.
        await NotifyReceiptAsync(payment, ct);
    }

    private async Task GenerateInvoiceAsync(Payment payment, CancellationToken ct)
    {
        // Only one invoice per payment (the column is unique in the schema).
        var exists = await _db.Invoices.AnyAsync(i => i.PaymentId == payment.Id, ct);
        if (exists)
        {
            return;
        }

        var issuedAt = _clock.UtcNow;
        var number = await _invoiceNumbers.NextAsync(issuedAt, ct);

        var netFee = PricingMath.Money(payment.Amount - payment.TaxGst);
        var lineItems = JsonSerializer.Serialize(new[]
        {
            new { description = "ITR filing fee", amount = netFee, gst = payment.TaxGst }
        });

        _db.Invoices.Add(new Invoice
        {
            TenantId = payment.TenantId,
            PaymentId = payment.Id,
            Number = number,
            Amount = netFee,
            Gst = payment.TaxGst,
            GstinSeller = "29ABCDE1234F1Z5", // STUB: our seller GSTIN (config-backed in production)
            PlaceOfSupply = "29",
            LineItemsJson = lineItems,
            IssuedAt = issuedAt,
            CreatedAt = issuedAt
        });
    }

    private async Task NotifyReceiptAsync(Payment payment, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == payment.UserId, ct);
        var destination = user?.Email ?? user?.MobileE164 ?? "unknown";
        var channel = user?.Email is not null ? NotificationChannel.Email : NotificationChannel.Sms;

        await _notifications.SendAsync(new NotificationMessage(
            channel,
            destination,
            "payment.receipt",
            "Your TallyG payment receipt",
            $"We received your payment of {payment.Amount:0.00} {payment.Currency}. Your return is now ready to file.",
            new Dictionary<string, string>
            {
                ["paymentId"] = payment.Id.ToString(),
                ["amount"] = payment.Amount.ToString("0.00")
            }), ct);
    }

    private async Task<Payment> LoadOwnedPaymentAsync(Guid paymentId, CancellationToken ct)
        => await _db.Payments.FirstOrDefaultAsync(
               p => p.Id == paymentId && p.TenantId == _currentUser.TenantId && p.UserId == _currentUser.UserId, ct)
           ?? throw AppException.NotFound("Payment not found.", "PAYMENT.NOT_FOUND");

    private async Task<TaxReturn?> LoadReturnAsync(Guid? taxReturnId, CancellationToken ct)
        => taxReturnId is { } id
            ? await _db.TaxReturns.FirstOrDefaultAsync(r => r.Id == id, ct)
            : null;

    private async Task<IReadOnlyList<PaymentDto>> ProjectAsync(IReadOnlyList<Payment> payments, CancellationToken ct)
    {
        var ids = payments.Select(p => p.Id).ToList();
        var invoices = await _db.Invoices
            .Where(i => ids.Contains(i.PaymentId))
            .ToDictionaryAsync(i => i.PaymentId, ct);

        return payments.Select(p =>
        {
            invoices.TryGetValue(p.Id, out var inv);
            return new PaymentDto(
                p.Id, p.TaxReturnId, p.Gateway.ToString(), p.GatewayOrderId, p.GatewayPaymentId,
                p.Amount, p.Currency, p.TaxGst, p.DiscountAmount, p.WalletApplied, p.Status.ToString(),
                inv?.Id, inv?.Number, p.CreatedAt);
        }).ToList();
    }

    private async Task<CreateOrderResponse> BuildOrderResponseAsync(
        Payment payment, CancellationToken ct, string? keyId = null, decimal? amountPayable = null)
    {
        var netFee = PricingMath.Money(payment.Amount - payment.TaxGst);
        var baseAmount = PricingMath.Money(netFee + payment.DiscountAmount);
        var payable = amountPayable ?? PricingMath.Money(payment.Amount - payment.WalletApplied);

        // The wallet leg captures instantly; everything else needs the checkout.
        var requiresCheckout = payment.Gateway != Gateway.Wallet && payment.Status != PaymentStatus.Paid;

        return await Task.FromResult(new CreateOrderResponse(
            payment.Id,
            payment.Gateway.ToString(),
            payment.GatewayOrderId,
            keyId,
            payment.Currency,
            baseAmount,
            payment.DiscountAmount,
            payment.WalletApplied,
            payment.TaxGst,
            payable,
            payment.Status.ToString(),
            requiresCheckout));
    }

    private async Task<VerifyPaymentResponse> BuildVerifyResponseAsync(Payment payment, CancellationToken ct)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.PaymentId == payment.Id, ct);
        var taxReturn = await LoadReturnAsync(payment.TaxReturnId, ct);

        return new VerifyPaymentResponse(
            payment.Id,
            payment.Status.ToString(),
            invoice?.Id,
            invoice?.Number,
            payment.TaxReturnId,
            taxReturn?.Status.ToString());
    }

    private static IReadOnlyList<string> ParseFeatures(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static Gateway ParseGateway(string? gateway, bool useWallet)
    {
        var value = (gateway ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "razorpay" or "" => Gateway.Razorpay,
            "cashfree" => Gateway.Cashfree,
            "wallet" => Gateway.Wallet,
            _ => throw AppException.Validation($"Unsupported gateway '{gateway}'.", "PAYMENT.GATEWAY_UNSUPPORTED")
        };
    }

    private static PaymentStatus NormalizeWebhookStatus(string? @event, string? status)
    {
        var token = (@event ?? status ?? string.Empty).Trim().ToLowerInvariant();
        return token switch
        {
            "payment.captured" or "captured" or "paid" or "success" or "order.paid" => PaymentStatus.Paid,
            "payment.failed" or "failed" or "cancelled" => PaymentStatus.Failed,
            _ => PaymentStatus.Pending
        };
    }
}
