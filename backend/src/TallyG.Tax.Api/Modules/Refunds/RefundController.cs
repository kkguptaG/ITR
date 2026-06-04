using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Refunds;

/// <summary>
/// Income-tax refund/demand tracking for a return. Once CPC processes the return, this reports the
/// determined refund (and its credit progress) or demand, and lets the assessee request a re-issue
/// after a failed credit. Owner-scoped (the service enforces it). Non-CRUD actions use ":verb".
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class RefundController : ControllerBase
{
    private readonly IRefundService _svc;

    public RefundController(IRefundService svc) => _svc = svc;

    /// <summary>Current refund/demand state of the return (reconciles processing + refund progress on read).</summary>
    [HttpGet("returns/{id:guid}/refund")]
    [ProducesResponseType(typeof(RefundStatusDto), StatusCodes.Status200OK)]
    public Task<RefundStatusDto> Get([FromRoute] Guid id, CancellationToken ct)
        => _svc.GetAsync(id, ct);

    /// <summary>Request a refund re-issue after a failed bank credit.</summary>
    [HttpPost("returns/{id:guid}/refund:reissue")]
    [ProducesResponseType(typeof(RefundStatusDto), StatusCodes.Status200OK)]
    public Task<RefundStatusDto> Reissue([FromRoute] Guid id, CancellationToken ct)
        => _svc.RequestReissueAsync(id, ct);
}
