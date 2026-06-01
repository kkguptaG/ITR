// Pure pricing arithmetic shared by the payment + coupon services.
// Kept side-effect-free and free of "Service" suffix so Scrutor does not try to DI-bind it.

using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Payments;

/// <summary>Pure helpers for discount + GST computation on the filing fee.</summary>
internal static class PricingMath
{
    /// <summary>GST rate applied to our service fee (18% — Ch.2 §2.7 "18% GST on our fee").</summary>
    public const decimal GstRate = 0.18m;

    /// <summary>Round to paisa (2 dp, banker's-rounding-free half-up to match invoice display).</summary>
    public static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Compute the discount a coupon grants against <paramref name="baseAmount"/>, honouring
    /// percent/flat type, the per-coupon max-discount cap and the min-order floor. Never returns
    /// more than the base amount (no negative net). Throws <see cref="AppException"/> on invalid use.
    /// </summary>
    public static decimal ComputeDiscount(Coupon coupon, decimal baseAmount, DateTimeOffset now)
    {
        if (!coupon.Active)
        {
            throw AppException.Validation("This coupon is not active.", "PAYMENT.COUPON_INACTIVE");
        }

        if (coupon.ExpiresAt is { } expiry && now >= expiry)
        {
            throw AppException.Validation("This coupon has expired.", "PAYMENT.COUPON_EXPIRED");
        }

        if (coupon.MaxRedemptions > 0 && coupon.Redeemed >= coupon.MaxRedemptions)
        {
            throw AppException.Validation("This coupon has reached its redemption limit.", "PAYMENT.COUPON_EXHAUSTED");
        }

        if (coupon.MinOrder is { } min && baseAmount < min)
        {
            throw AppException.Validation(
                $"This coupon requires a minimum order of {min:0.00}.", "PAYMENT.COUPON_MIN_ORDER");
        }

        var raw = coupon.Type switch
        {
            CouponType.Percent => baseAmount * (coupon.Value / 100m),
            CouponType.Flat => coupon.Value,
            _ => 0m
        };

        if (coupon.MaxDiscount is { } cap && raw > cap)
        {
            raw = cap;
        }

        // Never discount below zero.
        raw = Math.Clamp(raw, 0m, baseAmount);
        return Money(raw);
    }

    /// <summary>GST charged on the (post-discount, pre-wallet) net service fee.</summary>
    public static decimal Gst(decimal netFee) => Money(netFee * GstRate);
}
