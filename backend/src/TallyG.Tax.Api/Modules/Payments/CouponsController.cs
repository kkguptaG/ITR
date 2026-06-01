using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Payments;

/// <summary>
/// Coupon validation + application (docs 04 §4.2). Both actions use the ":verb" sub-resource
/// convention and are scoped to the authenticated user (apply targets the caller's own order).
/// </summary>
[ApiController]
[Route("api/v1/coupons")]
[Authorize]
public sealed class CouponsController : ControllerBase
{
    private readonly ICouponService _coupons;

    public CouponsController(ICouponService coupons) => _coupons = coupons;

    /// <summary>Validate a coupon against a plan and return the discounted price.</summary>
    [HttpPost(":validate")]
    [ProducesResponseType(typeof(CouponResultDto), StatusCodes.Status200OK)]
    public Task<CouponResultDto> Validate([FromBody] CouponValidateRequest request, CancellationToken ct)
        => _coupons.ValidateAsync(request, ct);

    /// <summary>Apply a coupon to an existing unpaid order, repricing it.</summary>
    [HttpPost(":apply")]
    [ProducesResponseType(typeof(CouponResultDto), StatusCodes.Status200OK)]
    public Task<CouponResultDto> Apply([FromBody] CouponApplyRequest request, CancellationToken ct)
        => _coupons.ApplyAsync(request, ct);
}
