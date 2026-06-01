using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Admin.Crm;

/// <summary>
/// CRM lead management (docs 07 §7.8). Admin/Ops within their tenant; SuperAdmin across all.
/// Stage transitions use the canonical ":stage" action sub-resource convention (Decision Log D-3);
/// activities are a POST to the /activities sub-collection. The /pipeline route is a literal
/// segment (never a Guid) so it does not collide with GET /{id}.
/// </summary>
[ApiController]
[Route("api/v1/admin/leads")]
[Authorize(Roles = AdminScope.Roles)]
public sealed class LeadsController : ControllerBase
{
    private readonly ILeadService _leads;

    public LeadsController(ILeadService leads) => _leads = leads;

    /// <summary>List/search leads (paged), optional stage filter.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<LeadDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<LeadDto>> List(
        [FromQuery] string? stage = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminPaging.DefaultPageSize,
        CancellationToken ct = default)
        => _leads.ListAsync(stage, search, page, pageSize, ct);

    /// <summary>The pipeline (kanban) view grouped by funnel stage.</summary>
    [HttpGet("pipeline")]
    [ProducesResponseType(typeof(PipelineDto), StatusCodes.Status200OK)]
    public Task<PipelineDto> Pipeline([FromQuery] int perStage = 20, CancellationToken ct = default)
        => _leads.GetPipelineAsync(perStage, ct);

    /// <summary>Get a lead with its activity timeline.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LeadDetailDto), StatusCodes.Status200OK)]
    public Task<LeadDetailDto> Get([FromRoute] Guid id, CancellationToken ct)
        => _leads.GetAsync(id, ct);

    /// <summary>Create a lead.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LeadDetailDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateLeadRequest request, CancellationToken ct)
    {
        var lead = await _leads.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = lead.Lead.Id }, lead);
    }

    /// <summary>Partial update of a lead's mutable fields.</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(LeadDetailDto), StatusCodes.Status200OK)]
    public Task<LeadDetailDto> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateLeadRequest request,
        CancellationToken ct)
        => _leads.UpdateAsync(id, request, ct);

    /// <summary>Delete a lead and its activities.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        await _leads.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Move the lead to a new funnel stage.</summary>
    [HttpPatch("{id:guid}:stage")]
    [ProducesResponseType(typeof(LeadDetailDto), StatusCodes.Status200OK)]
    public Task<LeadDetailDto> ChangeStage(
        [FromRoute] Guid id,
        [FromBody] UpdateLeadStageRequest request,
        CancellationToken ct)
        => _leads.ChangeStageAsync(id, request, ct);

    /// <summary>Append a CRM activity to the lead timeline.</summary>
    [HttpPost("{id:guid}/activities")]
    [ProducesResponseType(typeof(LeadActivityDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddActivity(
        [FromRoute] Guid id,
        [FromBody] AddLeadActivityRequest request,
        CancellationToken ct)
    {
        var activity = await _leads.AddActivityAsync(id, request, ct);
        return CreatedAtAction(nameof(Get), new { id }, activity);
    }
}
