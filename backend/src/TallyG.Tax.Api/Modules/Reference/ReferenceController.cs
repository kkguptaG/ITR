using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TallyG.Tax.Api.Modules.Reference;

/// <summary>
/// Securities reference lookups for the capital-gains form: ISIN → security (validate/auto-fill), and
/// the 31-Jan-2018 NSE FMV for s.112A grandfathering. Public reference data (no PII) but kept behind
/// auth like the rest of the app.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/reference")]
public sealed class ReferenceController : ControllerBase
{
    private readonly IIsinLookupService _isin;
    private readonly IGrandfatherFmvLookupService _fmv;
    private readonly ITdsCodeService _tds;

    public ReferenceController(IIsinLookupService isin, IGrandfatherFmvLookupService fmv, ITdsCodeService tds)
    {
        _isin = isin;
        _fmv = fmv;
        _tds = tds;
    }

    /// <summary>Resolve an ISIN to its security name + type; 404 if it isn't in the master.</summary>
    [HttpGet("isin/{code}")]
    [ProducesResponseType(typeof(IsinRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IsinRecord> Isin([FromRoute] string code)
    {
        var rec = _isin.Lookup(code);
        return rec is null ? NotFound() : Ok(rec);
    }

    /// <summary>The 31-Jan-2018 grandfathered FMV for an NSE symbol; 404 if not listed then.</summary>
    [HttpGet("grandfather-fmv/{symbol}")]
    [ProducesResponseType(typeof(GrandfatherFmvRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<GrandfatherFmvRecord> GrandfatherFmv([FromRoute] string symbol)
    {
        var rec = _fmv.Lookup(symbol);
        return rec is null ? NotFound() : Ok(rec);
    }

    /// <summary>Type-ahead search of NSE symbols (with their 31-Jan-2018 FMV) by symbol prefix.</summary>
    [HttpGet("grandfather-fmv")]
    [ProducesResponseType(typeof(IReadOnlyList<GrandfatherFmvRecord>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<GrandfatherFmvRecord>> SearchFmv([FromQuery] string q)
        => Ok(_fmv.Search(q ?? string.Empty));

    /// <summary>The ITD TDS section/deductee codes, for the section picker on a TDS-credit entry.</summary>
    [HttpGet("tds-codes")]
    [ProducesResponseType(typeof(IReadOnlyList<TdsCodeRecord>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TdsCodeRecord>> TdsCodes() => Ok(_tds.All());
}
