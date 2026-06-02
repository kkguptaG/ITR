using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Reconciliation;

/// <summary>
/// Reconcile a return against the department's records (AIS + Form 26AS) before filing. Owner-scoped in
/// the service. Read-only — it never mutates the return.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/returns/{returnId:guid}/reconciliation")]
public sealed class ReconciliationController : ControllerBase
{
    private readonly IReconciliationService _svc;

    public ReconciliationController(IReconciliationService svc) => _svc = svc;

    [HttpGet]
    [ProducesResponseType(typeof(ReconciliationReportDto), StatusCodes.Status200OK)]
    public Task<ReconciliationReportDto> Get([FromRoute] Guid returnId, CancellationToken ct)
        => _svc.ReconcileAsync(returnId, ct);
}
