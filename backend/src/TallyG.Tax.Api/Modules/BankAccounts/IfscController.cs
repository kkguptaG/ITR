using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.BankAccounts;

/// <summary>
/// IFSC → bank/branch lookup over the bundled RBI master, for auto-filling the bank-account form.
/// Public reference data (no PII), but kept behind auth like the rest of the app.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/ifsc")]
public sealed class IfscController : ControllerBase
{
    private readonly IIfscLookupService _ifsc;

    public IfscController(IIfscLookupService ifsc) => _ifsc = ifsc;

    /// <summary>Resolve an IFSC to its bank + branch; 404 if it isn't in the master.</summary>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(IfscRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IfscRecord> Get([FromRoute] string code)
    {
        var rec = _ifsc.Lookup(code);
        return rec is null ? NotFound() : Ok(rec);
    }
}
