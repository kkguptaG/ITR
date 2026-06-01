using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>A gateway payment intent/charge, idempotent on gateway order id (Ch.2 §2.7).</summary>
public class Payment : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? TaxReturnId { get; set; }
    public Guid? SubscriptionId { get; set; }

    public Gateway Gateway { get; set; }
    public string? GatewayOrderId { get; set; }
    public string? GatewayPaymentId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public decimal TaxGst { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal WalletApplied { get; set; }

    public Guid? CouponId { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Created;

    /// <summary>Last verified webhook payload (jsonb on Postgres, text on Sqlite).</summary>
    public string? WebhookPayloadJson { get; set; }

    public string? IdempotencyKey { get; set; }
}
