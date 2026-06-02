using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Accounting;

/// <summary>
/// Financial statements derived from the user's double-entry books — a Balance Sheet and Profit &amp;
/// Loss aggregated from the chart of accounts. Owner/tenant-scoped in the service. Feeds ITR-3.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/accounting/financial-statements")]
public sealed class FinancialStatementsController : ControllerBase
{
    private readonly IFinancialStatementsService _statements;

    public FinancialStatementsController(IFinancialStatementsService statements) => _statements = statements;

    /// <summary>Balance Sheet + P&amp;L derived from the posted ledgers.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(FinancialStatementsDto), StatusCodes.Status200OK)]
    public Task<FinancialStatementsDto> Get(CancellationToken ct = default) => _statements.GetAsync(ct);
}
