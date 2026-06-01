namespace TallyG.Tax.Domain.Enums;

/// <summary>Gateway payment lifecycle (Ch.2 payments).</summary>
public enum PaymentStatus
{
    Created = 0,
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Refunded = 4
}
