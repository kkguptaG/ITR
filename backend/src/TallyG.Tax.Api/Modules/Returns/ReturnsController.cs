using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Returns;

/// <summary>
/// Tax Returns / Filing endpoints (docs 04 §4.2). Thin actions delegating to
/// <see cref="IReturnService"/> / <see cref="IItrSelectorService"/>; DTO records in/out; errors via
/// <see cref="AppException"/> (rendered as RFC 7807 problem+json). Non-CRUD actions use the colon
/// sub-resource convention (e.g. <c>{id}:submit</c>) per the Decision Log. All endpoints require an
/// authenticated principal; the service scopes every row to the current user + tenant.
/// </summary>
[ApiController]
[Route("api/v1/returns")]
[Authorize]
public sealed class ReturnsController : ControllerBase
{
    private readonly IReturnService _returns;
    private readonly IItrSelectorService _selector;

    public ReturnsController(IReturnService returns, IItrSelectorService selector)
    {
        _returns = returns;
        _selector = selector;
    }

    // ------------------------------------------------------------- selector (stateless)

    /// <summary>Recommend an ITR form from a set of feature flags (answers → form). Stateless.</summary>
    [HttpGet("selector")]
    [ProducesResponseType(typeof(ItrSelectionVerdict), StatusCodes.Status200OK)]
    public ActionResult<ItrSelectionVerdict> Selector([FromQuery] ItrSelectorInput input)
        => Ok(_selector.Select(input));

    // ------------------------------------------------------------- return header CRUD

    /// <summary>List the current user's returns (filterable by ay/status/itrType).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReturnSummaryDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<ReturnSummaryDto>> List(
        [FromQuery] string? ay,
        [FromQuery] string? status,
        [FromQuery] string? itrType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _returns.ListAsync(ay, status, itrType, page, pageSize, ct);

    /// <summary>Create a draft return for an assessment year (ITR type optional / auto-selectable).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReturnDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ReturnDetailDto>> Create([FromBody] CreateReturnRequest request, CancellationToken ct)
    {
        var created = await _returns.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Full return detail incl. every income head, deductions, and the latest computation.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReturnDetailDto), StatusCodes.Status200OK)]
    public Task<ReturnDetailDto> Get(Guid id, CancellationToken ct) => _returns.GetAsync(id, ct);

    /// <summary>Update draft header fields (ITR type, regime, questionnaire answers).</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ReturnDetailDto), StatusCodes.Status200OK)]
    public Task<ReturnDetailDto> Update(Guid id, [FromBody] UpdateReturnRequest request, CancellationToken ct)
        => _returns.UpdateAsync(id, request, ct);

    /// <summary>Soft-delete a draft return.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _returns.DeleteAsync(id, ct);
        return NoContent();
    }

    // ------------------------------------------------------------- income sources

    [HttpGet("{id:guid}/income-sources")]
    [ProducesResponseType(typeof(IReadOnlyList<IncomeSourceDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<IncomeSourceDto>> ListIncomeSources(Guid id, CancellationToken ct)
        => _returns.ListIncomeSourcesAsync(id, ct);

    [HttpPost("{id:guid}/income-sources")]
    [ProducesResponseType(typeof(IncomeSourceDto), StatusCodes.Status200OK)]
    public Task<IncomeSourceDto> AddIncomeSource(Guid id, [FromBody] UpsertIncomeSourceRequest request, CancellationToken ct)
        => _returns.AddIncomeSourceAsync(id, request, ct);

    [HttpPatch("{id:guid}/income-sources/{sourceId:guid}")]
    [ProducesResponseType(typeof(IncomeSourceDto), StatusCodes.Status200OK)]
    public Task<IncomeSourceDto> UpdateIncomeSource(Guid id, Guid sourceId, [FromBody] UpsertIncomeSourceRequest request, CancellationToken ct)
        => _returns.UpdateIncomeSourceAsync(id, sourceId, request, ct);

    [HttpDelete("{id:guid}/income-sources/{sourceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteIncomeSource(Guid id, Guid sourceId, CancellationToken ct)
    {
        await _returns.DeleteIncomeSourceAsync(id, sourceId, ct);
        return NoContent();
    }

    // ------------------------------------------------------------- salary

    [HttpGet("{id:guid}/salary")]
    [ProducesResponseType(typeof(IReadOnlyList<SalaryDetailDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SalaryDetailDto>> ListSalaries(Guid id, CancellationToken ct)
        => _returns.ListSalariesAsync(id, ct);

    [HttpPost("{id:guid}/salary")]
    [ProducesResponseType(typeof(SalaryDetailDto), StatusCodes.Status200OK)]
    public Task<SalaryDetailDto> AddSalary(Guid id, [FromBody] UpsertSalaryRequest request, CancellationToken ct)
        => _returns.AddSalaryAsync(id, request, ct);

    [HttpPatch("{id:guid}/salary/{salaryId:guid}")]
    [ProducesResponseType(typeof(SalaryDetailDto), StatusCodes.Status200OK)]
    public Task<SalaryDetailDto> UpdateSalary(Guid id, Guid salaryId, [FromBody] UpsertSalaryRequest request, CancellationToken ct)
        => _returns.UpdateSalaryAsync(id, salaryId, request, ct);

    [HttpDelete("{id:guid}/salary/{salaryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSalary(Guid id, Guid salaryId, CancellationToken ct)
    {
        await _returns.DeleteSalaryAsync(id, salaryId, ct);
        return NoContent();
    }

    // ------------------------------------------------------------- house property

    [HttpGet("{id:guid}/house-property")]
    [ProducesResponseType(typeof(IReadOnlyList<HousePropertyDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<HousePropertyDto>> ListHouseProperties(Guid id, CancellationToken ct)
        => _returns.ListHousePropertiesAsync(id, ct);

    [HttpPost("{id:guid}/house-property")]
    [ProducesResponseType(typeof(HousePropertyDto), StatusCodes.Status200OK)]
    public Task<HousePropertyDto> AddHouseProperty(Guid id, [FromBody] UpsertHousePropertyRequest request, CancellationToken ct)
        => _returns.AddHousePropertyAsync(id, request, ct);

    [HttpPatch("{id:guid}/house-property/{propertyId:guid}")]
    [ProducesResponseType(typeof(HousePropertyDto), StatusCodes.Status200OK)]
    public Task<HousePropertyDto> UpdateHouseProperty(Guid id, Guid propertyId, [FromBody] UpsertHousePropertyRequest request, CancellationToken ct)
        => _returns.UpdateHousePropertyAsync(id, propertyId, request, ct);

    [HttpDelete("{id:guid}/house-property/{propertyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteHouseProperty(Guid id, Guid propertyId, CancellationToken ct)
    {
        await _returns.DeleteHousePropertyAsync(id, propertyId, ct);
        return NoContent();
    }

    // ------------------------------------------------------------- capital gains

    [HttpGet("{id:guid}/capital-gains")]
    [ProducesResponseType(typeof(IReadOnlyList<CapitalGainDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<CapitalGainDto>> ListCapitalGains(Guid id, CancellationToken ct)
        => _returns.ListCapitalGainsAsync(id, ct);

    [HttpPost("{id:guid}/capital-gains")]
    [ProducesResponseType(typeof(CapitalGainDto), StatusCodes.Status200OK)]
    public Task<CapitalGainDto> AddCapitalGain(Guid id, [FromBody] UpsertCapitalGainRequest request, CancellationToken ct)
        => _returns.AddCapitalGainAsync(id, request, ct);

    [HttpPatch("{id:guid}/capital-gains/{gainId:guid}")]
    [ProducesResponseType(typeof(CapitalGainDto), StatusCodes.Status200OK)]
    public Task<CapitalGainDto> UpdateCapitalGain(Guid id, Guid gainId, [FromBody] UpsertCapitalGainRequest request, CancellationToken ct)
        => _returns.UpdateCapitalGainAsync(id, gainId, request, ct);

    [HttpDelete("{id:guid}/capital-gains/{gainId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteCapitalGain(Guid id, Guid gainId, CancellationToken ct)
    {
        await _returns.DeleteCapitalGainAsync(id, gainId, ct);
        return NoContent();
    }

    // ------------------------------------------------------------- immovable-property buyers (s.194-IA)

    [HttpGet("{id:guid}/capital-gains/{gainId:guid}/buyers")]
    [ProducesResponseType(typeof(IReadOnlyList<CapitalGainBuyerDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<CapitalGainBuyerDto>> ListCapitalGainBuyers(Guid id, Guid gainId, CancellationToken ct)
        => _returns.ListCapitalGainBuyersAsync(id, gainId, ct);

    [HttpPost("{id:guid}/capital-gains/{gainId:guid}/buyers")]
    [ProducesResponseType(typeof(CapitalGainBuyerDto), StatusCodes.Status200OK)]
    public Task<CapitalGainBuyerDto> AddCapitalGainBuyer(Guid id, Guid gainId, [FromBody] UpsertCapitalGainBuyerRequest request, CancellationToken ct)
        => _returns.AddCapitalGainBuyerAsync(id, gainId, request, ct);

    [HttpDelete("{id:guid}/capital-gains/{gainId:guid}/buyers/{buyerId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteCapitalGainBuyer(Guid id, Guid gainId, Guid buyerId, CancellationToken ct)
    {
        await _returns.DeleteCapitalGainBuyerAsync(id, gainId, buyerId, ct);
        return NoContent();
    }

    // ------------------------------------------------------------- business income

    [HttpGet("{id:guid}/business-income")]
    [ProducesResponseType(typeof(IReadOnlyList<BusinessIncomeDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<BusinessIncomeDto>> ListBusinessIncomes(Guid id, CancellationToken ct)
        => _returns.ListBusinessIncomesAsync(id, ct);

    [HttpPost("{id:guid}/business-income")]
    [ProducesResponseType(typeof(BusinessIncomeDto), StatusCodes.Status200OK)]
    public Task<BusinessIncomeDto> AddBusinessIncome(Guid id, [FromBody] UpsertBusinessIncomeRequest request, CancellationToken ct)
        => _returns.AddBusinessIncomeAsync(id, request, ct);

    [HttpPatch("{id:guid}/business-income/{businessId:guid}")]
    [ProducesResponseType(typeof(BusinessIncomeDto), StatusCodes.Status200OK)]
    public Task<BusinessIncomeDto> UpdateBusinessIncome(Guid id, Guid businessId, [FromBody] UpsertBusinessIncomeRequest request, CancellationToken ct)
        => _returns.UpdateBusinessIncomeAsync(id, businessId, request, ct);

    [HttpDelete("{id:guid}/business-income/{businessId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteBusinessIncome(Guid id, Guid businessId, CancellationToken ct)
    {
        await _returns.DeleteBusinessIncomeAsync(id, businessId, ct);
        return NoContent();
    }

    // ------------------------------------------------------------- deductions

    [HttpGet("{id:guid}/deductions")]
    [ProducesResponseType(typeof(IReadOnlyList<DeductionDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<DeductionDto>> ListDeductions(Guid id, CancellationToken ct)
        => _returns.ListDeductionsAsync(id, ct);

    [HttpPost("{id:guid}/deductions")]
    [ProducesResponseType(typeof(DeductionDto), StatusCodes.Status200OK)]
    public Task<DeductionDto> AddDeduction(Guid id, [FromBody] UpsertDeductionRequest request, CancellationToken ct)
        => _returns.AddDeductionAsync(id, request, ct);

    [HttpPatch("{id:guid}/deductions/{deductionId:guid}")]
    [ProducesResponseType(typeof(DeductionDto), StatusCodes.Status200OK)]
    public Task<DeductionDto> UpdateDeduction(Guid id, Guid deductionId, [FromBody] UpsertDeductionRequest request, CancellationToken ct)
        => _returns.UpdateDeductionAsync(id, deductionId, request, ct);

    [HttpDelete("{id:guid}/deductions/{deductionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteDeduction(Guid id, Guid deductionId, CancellationToken ct)
    {
        await _returns.DeleteDeductionAsync(id, deductionId, ct);
        return NoContent();
    }

    // ------------------------------------------------------------- lifecycle actions

    /// <summary>Run pre-file completeness + business-rule validation, returning findings.</summary>
    [HttpPost("{id:guid}:validate")]
    [ProducesResponseType(typeof(ValidateReturnResponse), StatusCodes.Status200OK)]
    public Task<ValidateReturnResponse> Validate(Guid id, CancellationToken ct) => _returns.ValidateAsync(id, ct);

    /// <summary>Submit a Paid return to the ITD ERI (stub); idempotent; snapshots + acknowledges.</summary>
    [HttpPost("{id:guid}:submit")]
    [ProducesResponseType(typeof(SubmitReturnResponse), StatusCodes.Status200OK)]
    public Task<SubmitReturnResponse> Submit(Guid id, CancellationToken ct) => _returns.SubmitAsync(id, ct);

    /// <summary>The e-filing lifecycle status of the return.</summary>
    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(ReturnStatusDto), StatusCodes.Status200OK)]
    public Task<ReturnStatusDto> Status(Guid id, CancellationToken ct) => _returns.GetStatusAsync(id, ct);

    /// <summary>Auto-suggest the ITR form for a saved return from its persisted income heads.</summary>
    [HttpPost("{id:guid}:suggest-type")]
    [ProducesResponseType(typeof(ItrSelectionVerdict), StatusCodes.Status200OK)]
    public Task<ItrSelectionVerdict> SuggestType(Guid id, CancellationToken ct) => _returns.SuggestTypeAsync(id, ct);
}
