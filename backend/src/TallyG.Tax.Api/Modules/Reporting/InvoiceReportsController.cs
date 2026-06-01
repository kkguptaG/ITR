using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Reporting;

/// <summary>
/// Tax-invoice PDF download (docs 09 §9.2). User-scoped (owner-or-operator). Uses the ":pdf" action
/// sub-resource verb (Decision Log D-3) to coexist with the Payments module's
/// <c>GET /payments/{id}/invoice</c>, which returns the invoice as JSON — this route streams the
/// rendered PDF and stores a copy in the vault.
/// </summary>
[ApiController]
[Route("api/v1/payments")]
[Authorize]
public sealed class InvoiceReportsController : ControllerBase
{
    private readonly IReportingService _reporting;

    public InvoiceReportsController(IReportingService reporting) => _reporting = reporting;

    /// <summary>Download the GST tax-invoice PDF for a captured payment.</summary>
    [HttpGet("{id:guid}/invoice:pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> InvoicePdf([FromRoute] Guid id, CancellationToken ct)
    {
        var file = await _reporting.GetInvoiceAsync(id, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
