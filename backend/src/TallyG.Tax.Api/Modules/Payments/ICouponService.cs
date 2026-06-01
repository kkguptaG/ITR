using TallyG.Tax.Domain.Entities;

namespace TallyG.Tax.Api.Modules.Payments;

/// <summary>
/// Coupon validation + application against the filing fee (Ch.7 §7.7.8). Auto-registered scoped by
/// Scrutor (CouponService : ICouponService).
/// </summary>
public interface ICouponService
{
    /// <summary>POST /coupons:validate — price a coupon against a plan without persisting anything.</summary>
    Task<CouponResultDto> ValidateAsync(CouponValidateRequest request, CancellationToken ct = default);

    /// <summary>POST /coupons:apply — attach a coupon to an existing unpaid order, repricing it.</summary>
    Task<CouponResultDto> ApplyAsync(CouponApplyRequest request, CancellationToken ct = default);

    /// <summary>Look up an active coupon by code (case-insensitive), or throw 404. Shared with payments.</summary>
    Task<Coupon> ResolveAsync(string code, CancellationToken ct = default);
}
