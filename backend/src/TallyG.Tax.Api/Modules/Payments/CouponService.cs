using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Payments;

/// <summary>
/// Coupon validation + application. Discounts are %/flat with optional max-discount cap, min-order
/// floor, expiry and a global redemption limit (Ch.2 §2.7, Ch.7 §7.7.8). Validation is pure (no
/// writes); application reprices an existing unpaid order and links the coupon, with the actual
/// redemption count incremented only when the order is paid (in <c>PaymentService</c>).
/// </summary>
public sealed class CouponService : ICouponService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;

    public CouponService(AppDbContext db, ICurrentUser currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<CouponResultDto> ValidateAsync(CouponValidateRequest request, CancellationToken ct = default)
    {
        var coupon = await ResolveAsync(request.Code, ct);
        var plan = await ResolvePlanAsync(request.PlanCode, ct);

        var discount = PricingMath.ComputeDiscount(coupon, plan.Price, _clock.UtcNow);
        var net = PricingMath.Money(plan.Price - discount);

        return new CouponResultDto(
            coupon.Code, true, coupon.Type.ToString(), coupon.Value,
            plan.Price, discount, net, "Coupon is valid.");
    }

    public async Task<CouponResultDto> ApplyAsync(CouponApplyRequest request, CancellationToken ct = default)
    {
        var coupon = await ResolveAsync(request.Code, ct);

        var payment = await _db.Payments.FirstOrDefaultAsync(
            p => p.Id == request.PaymentId
                 && p.TenantId == _currentUser.TenantId
                 && p.UserId == _currentUser.UserId, ct)
            ?? throw AppException.NotFound("Payment order not found.", "PAYMENT.NOT_FOUND");

        if (payment.Status != PaymentStatus.Created && payment.Status != PaymentStatus.Pending)
        {
            throw AppException.Conflict("Coupon can only be applied to an unpaid order.", "PAYMENT.NOT_PENDING");
        }

        // Base = fee before discount. Reconstruct it from the stored net amount + any prior discount,
        // so re-applying a coupon replaces (not stacks) the previous one.
        var baseAmount = PricingMath.Money(payment.Amount + payment.DiscountAmount - payment.TaxGst);
        var discount = PricingMath.ComputeDiscount(coupon, baseAmount, _clock.UtcNow);
        var netFee = PricingMath.Money(baseAmount - discount);

        payment.CouponId = coupon.Id;
        payment.DiscountAmount = discount;
        payment.TaxGst = PricingMath.Gst(netFee);
        payment.Amount = PricingMath.Money(netFee + payment.TaxGst);

        await _db.SaveChangesAsync(ct);

        return new CouponResultDto(
            coupon.Code, true, coupon.Type.ToString(), coupon.Value,
            baseAmount, discount, netFee, "Coupon applied to order.");
    }

    public async Task<Coupon> ResolveAsync(string code, CancellationToken ct = default)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length == 0)
        {
            throw AppException.Validation("Coupon code is required.", "PAYMENT.COUPON_REQUIRED");
        }

        // Coupons are global (tenant_id NULL in the schema) or tenant-scoped; the Coupon entity is
        // not tenant-scoped in the demo model, so match purely on code.
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code.ToUpper() == normalized, ct)
            ?? throw AppException.NotFound($"Coupon '{normalized}' was not found.", "PAYMENT.COUPON_NOT_FOUND");

        return coupon;
    }

    private async Task<Plan> ResolvePlanAsync(string planCode, CancellationToken ct)
    {
        var normalized = (planCode ?? string.Empty).Trim();
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Code == normalized && p.IsActive, ct)
            ?? throw AppException.NotFound($"Plan '{normalized}' was not found.", "PAYMENT.PLAN_NOT_FOUND");

        return plan;
    }
}
