using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>Discount coupon (Ch.2 §2.7).</summary>
public class Coupon : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public CouponType Type { get; set; }

    /// <summary>Percent (0-100) or flat INR depending on <see cref="Type"/>.</summary>
    public decimal Value { get; set; }

    public decimal? MaxDiscount { get; set; }
    public decimal? MinOrder { get; set; }

    public int MaxRedemptions { get; set; }
    public int Redeemed { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
    public bool Active { get; set; } = true;
}
